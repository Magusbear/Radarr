using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport.Specifications
{
    // Movies are 24/25 fps (29.97 for some NTSC releases). Higher frame rates are almost always
    // frame interpolated uploads that do not mention it in the release name.
    public class NotInterpolatedSpecification : IImportDecisionEngineSpecification
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public NotInterpolatedSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            if (!_configService.RejectInterpolatedVideo)
            {
                return ImportSpecDecision.Accept();
            }

            if (localMovie.ExistingFile)
            {
                _logger.Debug("Existing file, skipping frame rate check");
                return ImportSpecDecision.Accept();
            }

            var frameRate = localMovie.MediaInfo?.VideoFps ?? 0;

            if (frameRate <= 0)
            {
                _logger.Debug("Frame rate unknown, skipping frame rate check");
                return ImportSpecDecision.Accept();
            }

            var maximumFrameRate = _configService.MaximumVideoFrameRate;

            if (frameRate > maximumFrameRate)
            {
                _logger.Debug("Frame rate {0} is above maximum of {1}, likely interpolated", frameRate, maximumFrameRate);
                return ImportSpecDecision.Reject(ImportRejectionReason.InterpolatedVideo, "Video is {0:0.###} fps, above the maximum of {1} fps (likely interpolated)", frameRate, maximumFrameRate);
            }

            return ImportSpecDecision.Accept();
        }
    }
}
