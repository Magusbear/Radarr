using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.MediaFiles.MovieImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Specifications
{
    [TestFixture]
    public class NotInterpolatedSpecificationFixture : CoreTest<NotInterpolatedSpecification>
    {
        private LocalMovie _localMovie;

        [SetUp]
        public void Setup()
        {
            _localMovie = new LocalMovie
            {
                Path = "/downloads/Movie.2023.1080p.WEB-DL.H.264-GRP/Movie.2023.1080p.WEB-DL.H.264-GRP.mkv",
                MediaInfo = new MediaInfoModel()
            };

            Mocker.GetMock<IConfigService>().SetupGet(s => s.RejectInterpolatedVideo).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(s => s.MaximumVideoFrameRate).Returns(30);
        }

        [TestCase(23.976)]
        [TestCase(24)]
        [TestCase(25)]
        [TestCase(29.97)]
        [TestCase(30)]
        public void should_accept_regular_frame_rates(decimal frameRate)
        {
            _localMovie.MediaInfo.VideoFps = frameRate;

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [TestCase(48)]
        [TestCase(50)]
        [TestCase(59.94)]
        [TestCase(60)]
        [TestCase(120)]
        public void should_reject_high_frame_rates(decimal frameRate)
        {
            _localMovie.MediaInfo.VideoFps = frameRate;

            var decision = Subject.IsSatisfiedBy(_localMovie, null);

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.InterpolatedVideo);
        }

        [Test]
        public void should_accept_high_frame_rate_below_configured_maximum()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.MaximumVideoFrameRate).Returns(60);
            _localMovie.MediaInfo.VideoFps = 48;

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_disabled()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.RejectInterpolatedVideo).Returns(false);
            _localMovie.MediaInfo.VideoFps = 60;

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_existing_files()
        {
            _localMovie.ExistingFile = true;
            _localMovie.MediaInfo.VideoFps = 60;

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_frame_rate_is_unknown()
        {
            _localMovie.MediaInfo = null;

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }
    }
}
