using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.History;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.CustomFormats
{
    // After a download is imported, compares the custom format score the release had when it was
    // grabbed (based on its name) with the score of the imported file (verified with media info).
    // If the file scores lower the release was mislabeled: it is blocklisted and a new search started.
    public class MediaInfoVerificationService : IHandleAsync<MovieFileImportedEvent>
    {
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IConfigService _configService;
        private readonly IHistoryService _historyService;
        private readonly IBlocklistService _blocklistService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public MediaInfoVerificationService(ICustomFormatCalculationService formatCalculator,
                                            IConfigService configService,
                                            IHistoryService historyService,
                                            IBlocklistService blocklistService,
                                            IManageCommandQueue commandQueueManager,
                                            Logger logger)
        {
            _formatCalculator = formatCalculator;
            _configService = configService;
            _historyService = historyService;
            _blocklistService = blocklistService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public void HandleAsync(MovieFileImportedEvent message)
        {
            var localMovie = message.MovieInfo;
            var movie = localMovie.Movie;

            if (!_configService.VerifyCustomFormatsWithMediaInfo || !message.NewDownload || message.DownloadId.IsNullOrWhiteSpace() || localMovie.MediaInfo == null || movie?.QualityProfile == null)
            {
                return;
            }

            var grabbedHistory = _historyService.FindByDownloadId(message.DownloadId)
                .Where(h => h.EventType == MovieHistoryEventType.Grabbed && h.MovieId == movie.Id)
                .MaxBy(h => h.Date);

            if (grabbedHistory == null || !int.TryParse(grabbedHistory.Data.GetValueOrDefault("customFormatScore"), out var grabbedScore))
            {
                return;
            }

            var verifiedScore = movie.QualityProfile.CalculateCustomFormatScore(_formatCalculator.ParseCustomFormat(localMovie));

            if (verifiedScore >= grabbedScore)
            {
                _logger.Debug("Custom format score of imported file verified for '{0}': {1} (grabbed with {2})", grabbedHistory.SourceTitle, verifiedScore, grabbedScore);
                return;
            }

            var reason = $"Custom format score dropped from {grabbedScore} to {verifiedScore} after media info verification";

            _logger.Warn("{0}: {1}. Blocklisting release", grabbedHistory.SourceTitle, reason);
            _blocklistService.Block(grabbedHistory, reason);

            if (movie.Monitored && movie.QualityProfile.UpgradeAllowed)
            {
                _logger.Info("Searching for a replacement for '{0}'", movie.Title);
                _commandQueueManager.Push(new MoviesSearchCommand { MovieIds = new List<int> { movie.Id } });
            }
        }
    }
}
