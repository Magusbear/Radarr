using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.BurnedInSubtitles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.MediaFiles.MovieImport.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Specifications
{
    [TestFixture]
    public class NoBurnedInChineseSubtitlesSpecificationFixture : CoreTest<NoBurnedInChineseSubtitlesSpecification>
    {
        private LocalMovie _localMovie;
        private DownloadClientItem _downloadClientItem;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());

            _localMovie = new LocalMovie
            {
                Path = "/downloads/Movie.2023.1080p.WEB-DL.H.264-GRP/Movie.2023.1080p.WEB-DL.H.264-GRP.mkv",
                Movie = new Movie { MovieMetadata = new MovieMetadata { OriginalLanguage = Language.English } },
                MediaInfo = new MediaInfoModel { RunTime = TimeSpan.FromMinutes(110), VideoFps = 23.976m }
            };

            _downloadClientItem = new DownloadClientItem();

            Mocker.GetMock<IConfigService>().SetupGet(s => s.RejectBurnedInChineseSubtitles).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(s => s.RejectInterpolatedVideo).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(s => s.MaximumVideoFrameRate).Returns(30);

            Mocker.GetMock<IDiskProvider>().Setup(s => s.GetFileSize(It.IsAny<string>())).Returns(4_000_000_000);
            Mocker.GetMock<IDiskProvider>().Setup(s => s.FileGetLastWrite(It.IsAny<string>())).Returns(new DateTime(2026, 10, 13));

            GivenDetectorResult(BurnedInSubtitleStatus.Detected);
        }

        private void GivenDetectorResult(BurnedInSubtitleStatus status)
        {
            Mocker.GetMock<IBurnedInSubtitleDetector>()
                .Setup(s => s.DetectChinese(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(new BurnedInSubtitleResult { Status = status, SampledFrames = 12, FramesWithText = status == BurnedInSubtitleStatus.Detected ? 7 : 0 });
        }

        private void VerifyNotChecked()
        {
            Mocker.GetMock<IBurnedInSubtitleDetector>()
                .Verify(s => s.DetectChinese(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never());
        }

        [Test]
        public void should_reject_download_with_burned_in_chinese_subtitles()
        {
            var decision = Subject.IsSatisfiedBy(_localMovie, _downloadClientItem);

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.BurnedInChineseSubtitles);
            decision.Message.Should().Contain("7 of 12");
        }

        [TestCase(BurnedInSubtitleStatus.NotDetected)]
        [TestCase(BurnedInSubtitleStatus.Unknown)]
        public void should_accept_when_not_detected(BurnedInSubtitleStatus status)
        {
            GivenDetectorResult(status);

            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_only_check_a_file_once()
        {
            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem);
            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem);

            Mocker.GetMock<IBurnedInSubtitleDetector>()
                .Verify(s => s.DetectChinese(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once());
        }

        [Test]
        public void should_not_check_when_disabled()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.RejectBurnedInChineseSubtitles).Returns(false);

            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem).Accepted.Should().BeTrue();
            VerifyNotChecked();
        }

        [Test]
        public void should_not_check_files_not_from_a_download_client()
        {
            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
            VerifyNotChecked();
        }

        [Test]
        public void should_not_check_existing_files()
        {
            _localMovie.ExistingFile = true;

            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem).Accepted.Should().BeTrue();
            VerifyNotChecked();
        }

        [Test]
        public void should_not_check_samples()
        {
            _localMovie.MediaInfo.RunTime = TimeSpan.FromMinutes(2);

            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem).Accepted.Should().BeTrue();
            VerifyNotChecked();
        }

        [Test]
        public void should_not_check_interpolated_files()
        {
            _localMovie.MediaInfo.VideoFps = 60;

            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem).Accepted.Should().BeTrue();
            VerifyNotChecked();
        }

        [TestCase(10)]
        [TestCase(8)]
        [TestCase(21)]
        public void should_not_check_chinese_japanese_or_korean_movies(int languageId)
        {
            _localMovie.Movie.MovieMetadata.Value.OriginalLanguage = Language.FindById(languageId);

            Subject.IsSatisfiedBy(_localMovie, _downloadClientItem).Accepted.Should().BeTrue();
            VerifyNotChecked();
        }
    }
}
