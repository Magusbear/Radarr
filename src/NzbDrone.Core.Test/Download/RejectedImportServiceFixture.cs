using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class RejectedImportServiceFixture : CoreTest<RejectedImportService>
    {
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            _trackedDownload = new TrackedDownload
            {
                State = TrackedDownloadState.ImportPending,
                DownloadItem = new DownloadClientItem { Title = "Movie.2023.1080p.WEB-DL.H.264-GRP" },
                RemoteMovie = new RemoteMovie { Release = new ReleaseInfo { IndexerId = 1 } }
            };
        }

        private ImportResult GivenRejectedResult(ImportRejectionReason reason, string message)
        {
            return new ImportResult(new ImportDecision(new LocalMovie(), new ImportRejection(reason, message)), message);
        }

        [Test]
        public void should_fail_interpolated_download_with_reason()
        {
            var result = GivenRejectedResult(ImportRejectionReason.InterpolatedVideo, "Video is 60 fps, above the maximum of 30 fps (likely interpolated)");

            Subject.Process(_trackedDownload, result).Should().BeTrue();

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Error);
            _trackedDownload.FailureMessage.Should().Contain("60 fps");
        }

        [Test]
        public void should_fail_download_with_burned_in_chinese_subtitles_with_reason()
        {
            var result = GivenRejectedResult(ImportRejectionReason.BurnedInChineseSubtitles, "Burned-in Chinese subtitles detected (Chinese text in 7 of 12 sampled frames)");

            Subject.Process(_trackedDownload, result).Should().BeTrue();

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
            _trackedDownload.FailureMessage.Should().Contain("Burned-in Chinese subtitles");
        }

        [Test]
        public void should_only_warn_for_other_rejections_without_indexer_settings()
        {
            var result = GivenRejectedResult(ImportRejectionReason.Sample, "Sample");

            Subject.Process(_trackedDownload, result).Should().BeTrue();

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }
    }
}
