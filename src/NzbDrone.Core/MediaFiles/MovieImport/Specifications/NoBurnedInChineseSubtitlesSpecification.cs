using System;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.BurnedInSubtitles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport.Specifications
{
    public class NoBurnedInChineseSubtitlesSpecification : IImportDecisionEngineSpecification
    {
        private static readonly TimeSpan MinimumRuntime = TimeSpan.FromMinutes(10);

        private readonly IBurnedInSubtitleDetector _detector;
        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;
        private readonly ICached<BurnedInSubtitleResult> _cache;
        private readonly Logger _logger;

        public NoBurnedInChineseSubtitlesSpecification(IBurnedInSubtitleDetector detector,
                                                       IConfigService configService,
                                                       IDiskProvider diskProvider,
                                                       ICacheManager cacheManager,
                                                       Logger logger)
        {
            _detector = detector;
            _configService = configService;
            _diskProvider = diskProvider;
            _cache = cacheManager.GetCache<BurnedInSubtitleResult>(GetType());
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            if (!_configService.RejectBurnedInChineseSubtitles)
            {
                return ImportSpecDecision.Accept();
            }

            // Only check downloads, the check takes a while and would slow down disk scans and manual imports of folders
            if (localMovie.ExistingFile || downloadClientItem == null)
            {
                return ImportSpecDecision.Accept();
            }

            var mediaInfo = localMovie.MediaInfo;

            if (mediaInfo == null || mediaInfo.RunTime < MinimumRuntime)
            {
                _logger.Debug("No media info or runtime below {0} minutes, skipping burned-in subtitle check", MinimumRuntime.TotalMinutes);
                return ImportSpecDecision.Accept();
            }

            if (_configService.RejectInterpolatedVideo && mediaInfo.VideoFps > _configService.MaximumVideoFrameRate)
            {
                _logger.Debug("File is rejected as interpolated, skipping burned-in subtitle check");
                return ImportSpecDecision.Accept();
            }

            var originalLanguage = localMovie.Movie?.MovieMetadata?.Value?.OriginalLanguage;

            if (originalLanguage == Language.Chinese || originalLanguage == Language.Japanese || originalLanguage == Language.Korean)
            {
                _logger.Debug("Original language is {0}, skipping burned-in Chinese subtitle check", originalLanguage);
                return ImportSpecDecision.Accept();
            }

            var cacheKey = $"{localMovie.Path}|{_diskProvider.GetFileSize(localMovie.Path)}|{_diskProvider.FileGetLastWrite(localMovie.Path).Ticks}";
            var result = _cache.Get(cacheKey, () => _detector.DetectChinese(localMovie.Path, mediaInfo.RunTime), TimeSpan.FromDays(1));

            if (result.Status == BurnedInSubtitleStatus.Unknown)
            {
                return ImportSpecDecision.Accept();
            }

            _logger.Info("Burned-in subtitle check for {0}: Chinese text in {1} of {2} sampled frames", localMovie.Path, result.FramesWithText, result.SampledFrames);

            if (result.Status == BurnedInSubtitleStatus.Detected)
            {
                return ImportSpecDecision.Reject(ImportRejectionReason.BurnedInChineseSubtitles,
                    "Burned-in Chinese subtitles detected (Chinese text in {0} of {1} sampled frames)",
                    result.FramesWithText,
                    result.SampledFrames);
            }

            return ImportSpecDecision.Accept();
        }
    }
}
