using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class VerifiedReleaseTitleBuilderFixture : TestBase
    {
        private MediaInfoModel _hdr10Eac3;
        private MediaInfoModel _dvTrueHdAtmos;

        [SetUp]
        public void Setup()
        {
            _hdr10Eac3 = new MediaInfoModel
            {
                SchemaRevision = VideoFileInfoReader.CURRENT_MEDIA_INFO_SCHEMA_REVISION,
                VideoFormat = "hevc",
                VideoBitDepth = 10,
                VideoHdrFormat = HdrFormat.Hdr10,
                AudioStreams = new List<MediaInfoAudioStreamModel>
                {
                    new MediaInfoAudioStreamModel { Format = "eac3", ChannelPositions = "5.1(side)", Channels = 6 }
                }
            };

            _dvTrueHdAtmos = new MediaInfoModel
            {
                SchemaRevision = VideoFileInfoReader.CURRENT_MEDIA_INFO_SCHEMA_REVISION,
                VideoFormat = "hevc",
                VideoBitDepth = 10,
                VideoHdrFormat = HdrFormat.DolbyVisionHdr10,
                AudioStreams = new List<MediaInfoAudioStreamModel>
                {
                    new MediaInfoAudioStreamModel { Format = "truehd", CodecId = "thd+", Channels = 8 }
                }
            };
        }

        [Test]
        public void should_replace_unverified_tokens_with_media_info_tokens()
        {
            VerifiedReleaseTitleBuilder.Build("Movie.2023.2160p.WEB-DL.DV.HDR10.DDP5.1.Atmos.H.265-GRP", _hdr10Eac3, "GRP")
                .Should().Be("Movie.2023.2160p.WEB-DL.HDR10.h265.HEVC.10bit.DDP.EAC3.5.1-GRP");
        }

        [Test]
        public void should_keep_tokens_confirmed_by_media_info()
        {
            VerifiedReleaseTitleBuilder.Build("Movie.2023.2160p.UHD.BluRay.REMUX.DV.HDR10.TrueHD.7.1.Atmos.HEVC-GRP", _dvTrueHdAtmos, "GRP")
                .Should().Be("Movie.2023.2160p.UHD.BluRay.REMUX.DV.HDR10.HEVC.x265.10bit.TrueHD.Atmos.7.1-GRP");
        }

        [Test]
        public void should_handle_space_separated_file_names_with_extension()
        {
            var mediaInfo = new MediaInfoModel
            {
                SchemaRevision = VideoFileInfoReader.CURRENT_MEDIA_INFO_SCHEMA_REVISION,
                VideoFormat = "hevc",
                VideoBitDepth = 10,
                VideoHdrFormat = HdrFormat.Hdr10
            };

            VerifiedReleaseTitleBuilder.Build("Movie (2023) [2160p WEBDL DV HDR10 x265 10bit].mkv", mediaInfo)
                .Should().Be("Movie (2023) [2160p WEBDL] HDR10 x265 HEVC 10bit.mkv");
        }

        [Test]
        public void should_not_touch_tokens_in_movie_title()
        {
            var mediaInfo = new MediaInfoModel
            {
                SchemaRevision = VideoFileInfoReader.CURRENT_MEDIA_INFO_SCHEMA_REVISION,
                VideoFormat = "h264",
                VideoBitDepth = 8
            };

            VerifiedReleaseTitleBuilder.Build("DV.Movie.2023.1080p.BluRay.x264-GRP", mediaInfo, "GRP")
                .Should().Be("DV.Movie.2023.1080p.BluRay.x264.AVC.8bit-GRP");
        }

        [Test]
        public void should_not_strip_categories_without_media_info_data()
        {
            var mediaInfo = new MediaInfoModel
            {
                SchemaRevision = VideoFileInfoReader.CURRENT_MEDIA_INFO_SCHEMA_REVISION,
                VideoHdrFormat = HdrFormat.Hdr10
            };

            VerifiedReleaseTitleBuilder.Build("Movie.2023.2160p.WEB-DL.DV.HDR10.DDP5.1.H.265-GRP", mediaInfo, "GRP")
                .Should().Be("Movie.2023.2160p.WEB-DL.DDP5.1.H.265.HDR10-GRP");
        }

        [Test]
        public void should_return_title_unchanged_without_media_info()
        {
            VerifiedReleaseTitleBuilder.Build("Movie.2023.2160p.WEB-DL.DV.HDR10.H.265-GRP", null)
                .Should().Be("Movie.2023.2160p.WEB-DL.DV.HDR10.H.265-GRP");
        }

        [Test]
        public void should_return_title_unchanged_with_outdated_media_info()
        {
            _hdr10Eac3.SchemaRevision = VideoFileInfoReader.MINIMUM_MEDIA_INFO_SCHEMA_REVISION - 1;

            VerifiedReleaseTitleBuilder.Build("Movie.2023.2160p.WEB-DL.DV.HDR10.H.265-GRP", _hdr10Eac3, "GRP")
                .Should().Be("Movie.2023.2160p.WEB-DL.DV.HDR10.H.265-GRP");
        }
    }
}
