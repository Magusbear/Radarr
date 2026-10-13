using System.Linq;
using NLog;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MediaFiles.MovieImport;

namespace NzbDrone.Core.Download;

public interface IRejectedImportService
{
    bool Process(TrackedDownload trackedDownload, ImportResult importResult);
}

public static class FailingImportRejections
{
    // Rejections that mean the release itself is bad, so the download is failed (blocklisted and searched again)
    public static readonly ImportRejectionReason[] Reasons =
    {
        ImportRejectionReason.InterpolatedVideo,
        ImportRejectionReason.BurnedInChineseSubtitles
    };

    public static ImportRejection Find(ImportResult importResult)
    {
        return importResult.ImportDecision.Rejections.FirstOrDefault(r => Reasons.Contains(r.Reason));
    }
}

public class RejectedImportService : IRejectedImportService
{
    private readonly ICachedIndexerSettingsProvider _cachedIndexerSettingsProvider;
    private readonly Logger _logger;

    public RejectedImportService(ICachedIndexerSettingsProvider cachedIndexerSettingsProvider, Logger logger)
    {
        _cachedIndexerSettingsProvider = cachedIndexerSettingsProvider;
        _logger = logger;
    }

    public bool Process(TrackedDownload trackedDownload, ImportResult importResult)
    {
        if (importResult.Result != ImportResultType.Rejected || trackedDownload.RemoteMovie?.Release == null)
        {
            return false;
        }

        var failingRejection = FailingImportRejections.Find(importResult);

        if (failingRejection != null)
        {
            _logger.Info("Download '{0}' rejected ({1}), marking as failed", trackedDownload.DownloadItem.Title, failingRejection.Message);
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, failingRejection.Message));
            trackedDownload.Fail(failingRejection.Message);

            return true;
        }

        var indexerSettings = _cachedIndexerSettingsProvider.GetSettings(trackedDownload.RemoteMovie.Release.IndexerId);
        var rejectionReason = importResult.ImportDecision.Rejections.FirstOrDefault()?.Reason;

        if (indexerSettings == null)
        {
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, importResult.Errors));
            return true;
        }

        if (rejectionReason == ImportRejectionReason.DangerousFile &&
            indexerSettings.FailDownloads.Contains(FailDownloads.PotentiallyDangerous))
        {
            _logger.Trace("Download '{0}' contains potentially dangerous file, marking as failed", trackedDownload.DownloadItem.Title);
            trackedDownload.Fail();
        }
        else if (rejectionReason == ImportRejectionReason.ExecutableFile &&
            indexerSettings.FailDownloads.Contains(FailDownloads.Executables))
        {
            _logger.Trace("Download '{0}' contains executable file, marking as failed", trackedDownload.DownloadItem.Title);
            trackedDownload.Fail();
        }
        else
        {
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, importResult.Errors));
        }

        return true;
    }
}
