using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class CustomFormatCalculationServiceFixture : CoreTest<CustomFormatCalculationService>
    {
        private CustomFormat _dolbyVision;
        private Movie _movie;
        private MovieFile _movieFile;

        [SetUp]
        public void Setup()
        {
            _dolbyVision = new CustomFormat("DV", new ReleaseTitleSpecification { Value = @"\b(DV|DoVi|Dolby[ .]?Vision)\b" }) { Id = 1 };

            Mocker.GetMock<ICustomFormatService>()
                .Setup(s => s.All())
                .Returns(new List<CustomFormat> { _dolbyVision });

            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.VerifyCustomFormatsWithMediaInfo)
                .Returns(true);

            _movie = new Movie();

            _movieFile = new MovieFile
            {
                SceneName = "Movie.2023.2160p.WEB-DL.DV.HDR10.DDP5.1.H.265-GRP",
                RelativePath = "Movie (2023) [WEBDL-2160p].mkv",
                ReleaseGroup = "GRP",
                Quality = new QualityModel(Quality.WEBDL2160p)
            };
        }

        private void GivenMediaInfo(HdrFormat hdrFormat)
        {
            _movieFile.MediaInfo = new MediaInfoModel
            {
                SchemaRevision = VideoFileInfoReader.CURRENT_MEDIA_INFO_SCHEMA_REVISION,
                VideoFormat = "hevc",
                VideoBitDepth = 10,
                VideoHdrFormat = hdrFormat
            };
        }

        [Test]
        public void should_match_release_title_format_from_name_without_media_info()
        {
            Subject.ParseCustomFormat(_movieFile, _movie).Should().Contain(_dolbyVision);
        }

        [Test]
        public void should_match_release_title_format_confirmed_by_media_info()
        {
            GivenMediaInfo(HdrFormat.DolbyVisionHdr10);

            Subject.ParseCustomFormat(_movieFile, _movie).Should().Contain(_dolbyVision);
        }

        [Test]
        public void should_not_match_release_title_format_contradicted_by_media_info()
        {
            GivenMediaInfo(HdrFormat.Hdr10);

            Subject.ParseCustomFormat(_movieFile, _movie).Should().NotContain(_dolbyVision);
        }

        [Test]
        public void should_match_release_title_format_from_name_when_verification_is_disabled()
        {
            GivenMediaInfo(HdrFormat.Hdr10);

            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.VerifyCustomFormatsWithMediaInfo)
                .Returns(false);

            Subject.ParseCustomFormat(_movieFile, _movie).Should().Contain(_dolbyVision);
        }
    }
}
