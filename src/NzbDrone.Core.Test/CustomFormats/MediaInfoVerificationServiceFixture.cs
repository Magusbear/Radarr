using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class MediaInfoVerificationServiceFixture : CoreTest<MediaInfoVerificationService>
    {
        private CustomFormat _dolbyVision;
        private Movie _movie;
        private LocalMovie _localMovie;
        private MovieHistory _grabbed;

        [SetUp]
        public void Setup()
        {
            _dolbyVision = new CustomFormat("DV") { Id = 1 };

            _movie = Builder<Movie>.CreateNew()
                .With(m => m.Id = 5)
                .With(m => m.Monitored = true)
                .With(m => m.QualityProfile = new QualityProfile
                {
                    UpgradeAllowed = true,
                    FormatItems = new List<ProfileFormatItem> { new ProfileFormatItem { Format = _dolbyVision, Score = 100 } }
                })
                .Build();

            _localMovie = new LocalMovie
            {
                Movie = _movie,
                MediaInfo = new MediaInfoModel()
            };

            _grabbed = new MovieHistory
            {
                MovieId = _movie.Id,
                SourceTitle = "Movie.2023.2160p.WEB-DL.DV.H.265-GRP",
                EventType = MovieHistoryEventType.Grabbed,
                Date = DateTime.UtcNow,
                DownloadId = "abc"
            };
            _grabbed.Data.Add("customFormatScore", "100");

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId("abc"))
                .Returns(new List<MovieHistory> { _grabbed });

            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.VerifyCustomFormatsWithMediaInfo)
                .Returns(true);
        }

        private void GivenImportedFileFormats(params CustomFormat[] formats)
        {
            Mocker.GetMock<ICustomFormatCalculationService>()
                .Setup(s => s.ParseCustomFormat(_localMovie))
                .Returns(new List<CustomFormat>(formats));
        }

        private MovieFileImportedEvent GivenImportEvent(bool newDownload = true)
        {
            var downloadClientItem = new DownloadClientItem { DownloadId = "abc" };

            return new MovieFileImportedEvent(_localMovie, new MovieFile(), new List<DeletedMovieFile>(), newDownload, downloadClientItem);
        }

        [Test]
        public void should_blocklist_and_search_when_score_dropped()
        {
            GivenImportedFileFormats();

            Subject.HandleAsync(GivenImportEvent());

            Mocker.GetMock<IBlocklistService>()
                .Verify(s => s.Block(_grabbed, It.IsAny<string>()), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.Is<MoviesSearchCommand>(c => c.MovieIds.Contains(_movie.Id)), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once());
        }

        [Test]
        public void should_do_nothing_when_score_is_confirmed()
        {
            GivenImportedFileFormats(_dolbyVision);

            Subject.HandleAsync(GivenImportEvent());

            Mocker.GetMock<IBlocklistService>()
                .Verify(s => s.Block(It.IsAny<MovieHistory>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_blocklist_but_not_search_for_unmonitored_movie()
        {
            _movie.Monitored = false;
            GivenImportedFileFormats();

            Subject.HandleAsync(GivenImportEvent());

            Mocker.GetMock<IBlocklistService>()
                .Verify(s => s.Block(_grabbed, It.IsAny<string>()), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.IsAny<MoviesSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_ignore_imports_that_are_not_new_downloads()
        {
            GivenImportedFileFormats();

            Subject.HandleAsync(GivenImportEvent(false));

            Mocker.GetMock<IBlocklistService>()
                .Verify(s => s.Block(It.IsAny<MovieHistory>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_when_verification_is_disabled()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.VerifyCustomFormatsWithMediaInfo)
                .Returns(false);

            GivenImportedFileFormats();

            Subject.HandleAsync(GivenImportEvent());

            Mocker.GetMock<IBlocklistService>()
                .Verify(s => s.Block(It.IsAny<MovieHistory>(), It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.IsAny<MoviesSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_ignore_imports_without_media_info()
        {
            _localMovie.MediaInfo = null;
            GivenImportedFileFormats();

            Subject.HandleAsync(GivenImportEvent());

            Mocker.GetMock<IBlocklistService>()
                .Verify(s => s.Block(It.IsAny<MovieHistory>(), It.IsAny<string>()), Times.Never());
        }
    }
}
