# Radarr

[![Build Status](https://dev.azure.com/Radarr/Radarr/_apis/build/status/Radarr.Radarr?branchName=develop)](https://dev.azure.com/Radarr/Radarr/_build/latest?definitionId=1&branchName=develop)
[![Translation status](https://translate.servarr.com/widget/servarr/radarr/svg-badge.svg)](https://translate.servarr.com/engage/servarr/?utm_source=widget)
[![Docker Pulls](https://img.shields.io/docker/pulls/linuxserver/radarr.svg)](https://wiki.servarr.com/radarr/installation/docker)
![Github Downloads](https://img.shields.io/github/downloads/Radarr/Radarr/total.svg)
[![Backers on Open Collective](https://opencollective.com/Radarr/backers/badge.svg)](#backers)
[![Sponsors on Open Collective](https://opencollective.com/Radarr/sponsors/badge.svg)](#sponsors)
[![Mega Sponsors on Open Collective](https://opencollective.com/Radarr/megasponsors/badge.svg)](#mega-sponsors)

Radarr is a movie collection manager for Usenet and BitTorrent users. It can monitor multiple RSS feeds for new movies and will interface with clients and indexers to grab, sort, and rename them. It can also be configured to automatically upgrade the quality of existing files in the library when a better quality format becomes available.
Note that only one type of a given movie is supported. If you want both a 4k version and 1080p version of a given movie you will need multiple instances.

## About This Fork

This is a fork of [Radarr/Radarr](https://github.com/Radarr/Radarr) with the additional features listed below. Everything else works like upstream Radarr.

### Fork Features

#### Custom formats verified with media info

Upstream Radarr scores custom formats only from the release name, so a release called `...DV.HDR10.Atmos...` gets the Dolby Vision and Atmos scores even if the file has neither. This fork re-checks downloaded files with ffprobe:

* **Verified scoring of files:** For downloaded and existing files, technical tags in the release name (HDR / Dolby Vision / HDR10+ / HLG, video codec, bit depth, audio codec, audio channels, 3D) are replaced with what ffprobe actually found in the file before *Release Title* custom format conditions are evaluated. Your existing custom formats (e.g. from the TRaSH guides) keep working without changes.
  * Example: `Movie.2023.2160p.WEB-DL.DV.HDR10.DDP5.1.Atmos.H.265-GRP` that really contains HDR10 and plain DD+ 5.1 is scored as `Movie.2023.2160p.WEB-DL.HDR10.h265.HEVC.10bit.DDP.EAC3.5.1-GRP`, so it no longer matches DV or Atmos formats.
* **Mislabeled releases are replaced:** After an import, the file's verified score is compared with the score the release had when it was grabbed. If it dropped, the release is added to the blocklist (with the reason) and, if the movie is monitored and upgrades are allowed, a new search is started.
* **Upgrades use the real score:** Because existing files are scored with verified information, files whose names overstated their quality become eligible for upgrades through normal RSS sync and searches.
* **Setting:** *Settings → Media Management → Verify Custom Formats with Media Info* (on by default). Turning it off restores upstream behaviour.
* **Limitations:** Only applies once a file has been analyzed by ffprobe; release names are still used before download. Only the first audio track is checked. Tokens that are part of the movie title (before the year) are never changed.
* **Troubleshooting:** With trace logging, `Using media info verified release title: <name> -> <verified name>` shows the rewritten name. Blocklisted releases show the reason `Custom format score dropped from X to Y after media info verification`.

#### Reject interpolated video

Some uploads are frame-interpolated (e.g. a 24 fps movie converted to 60 fps) without saying so in the release name. This fork checks the real frame rate with ffprobe when a download is imported:

* Files above the maximum frame rate (default **30 fps**) are not imported. Regular movies (23.976, 24, 25 and 29.97 fps) are not affected.
* The download is marked as **failed**, so Radarr's failed download handling takes over: the release is blocklisted with the reason (e.g. `Video is 59.94 fps, above the maximum of 30 fps (likely interpolated)`), removed from the download client if *Remove* is enabled for the client, and a new search is started if *Redownload Failed* is enabled.
* Works for downloads that also contain a sample file.
* Files already in the library are never removed. A rejected file can still be imported through *Manual Import*.
* **Settings:** *Settings → Media Management → Reject Interpolated Video* (on by default) and *Maximum Frame Rate*. Raise the maximum if you want genuine high frame rate releases (e.g. 48 fps HFR).

#### Reject burned-in Chinese subtitles

Some uploads have Chinese subtitles burned into the picture without saying so in the release name. These cannot be seen in the file's metadata, so this fork reads the subtitle area with OCR when a download is imported:

* 12 frames spread between 10% and 90% of the movie are extracted with ffmpeg. The bottom part of each frame is converted to black text on white and read with tesseract (simplified and traditional Chinese).
* If 3 or more frames contain Chinese text, the file is not imported and the download is marked as **failed**: the release is blocklisted with the reason (e.g. `Burned-in Chinese subtitles detected (Chinese text in 7 of 12 sampled frames)`) and a new search is started if *Redownload Failed* is enabled.
* Every checked download logs the result, e.g. `Burned-in subtitle check for <file>: Chinese text in 0 of 12 sampled frames`.
* Not applied to movies whose original language is Chinese, Japanese or Korean, to files already in the library, to samples, or to manual imports of folders. Takes roughly 10–30 seconds per download.
* **Setting:** *Settings → Media Management → Reject Burned-in Chinese Subtitles* (on by default).
* Requires `ffmpeg` and `tesseract` with Chinese language data, which are included in the Docker image. Without them the check is skipped and a warning is logged.

#### Docker image

A multi-arch (amd64, arm64) Alpine image is published to `ghcr.io/magusbear/radarr` by the manually triggered *Publish Docker image* GitHub Action (Actions → Publish Docker image → Run workflow).

```
docker run -d -p 7878:7878 -v /path/to/config:/config -v /path/to/movies:/movies ghcr.io/magusbear/radarr:latest
```

* Runs as UID/GID 1000 (override with `--user <uid>:<gid>`); data is stored in `/config`.
* Updates are done by pulling a new image; Radarr's built-in updater is disabled.
* Tags: `latest`, the version (e.g. `10.0.0.6`) and `sha-<commit>`.

## Major Features Include

* Adding new movies with lots of information, such as trailers, ratings, etc.
* Support for major platforms: Windows, Linux, macOS, Raspberry Pi, etc.
* Can watch for better quality of the movies you have and do an automatic upgrade. _eg. from DVD to Blu-Ray_
* Automatic failed download handling will try another release if one fails
* Manual search so you can pick any release or to see why a release was not downloaded automatically
* Full integration with SABnzbd and NZBGet
* Automatically searching for releases as well as RSS Sync
* Automatically importing downloaded movies
* Recognizing Special Editions, Director's Cut, etc.
* Identifying releases with hardcoded subs
* Identifying releases with AKA movie names
* SABnzbd, NZBGet, QBittorrent, Deluge, rTorrent, Transmission, uTorrent, and other download clients are supported and integrated
* Full integration with Kodi and Plex (notifications, library updates)
* Importing Metadata such as trailers or subtitles
* Adding metadata such as posters and information for Kodi and others to use
* Advanced customization for profiles, such that Radarr will always download the copy you want
* A beautiful UI

## Support

[![Wiki](https://img.shields.io/badge/servarr-wiki-181717.svg?maxAge=60)](https://wiki.servarr.com/radarr)
[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://radarr.video/discord)

Note: GitHub Issues are for Bugs and Feature Requests Only

[![GitHub - Bugs and Feature Requests Only](https://img.shields.io/badge/github-issues-red.svg?maxAge=60)](https://github.com/Radarr/Radarr/issues)

## Contributors & Developers

[API Documentation](https://radarr.video/docs/api/)

This project exists thanks to all the people who contribute.
- [Contribute (GitHub)](CONTRIBUTING.md)
- [Contribution (Wiki Article)](https://wiki.servarr.com/radarr/contributing)

[![Contributors List](https://opencollective.com/Radarr/contributors.svg?width=890&button=false)](https://github.com/Radarr/Radarr/graphs/contributors)

## Backers

Thank you to all our backers! 🙏 [Become a backer](https://opencollective.com/Radarr#backer)

[![Backers List](https://opencollective.com/Radarr/backers.svg?width=890)](https://opencollective.com/Radarr#backer)

## Sponsors

Support this project by becoming a sponsor. Your logo will show up here with a link to your website. [Become a sponsor](https://opencollective.com/Radarr#sponsor)

[![Sponsors List](https://opencollective.com/Radarr/sponsors.svg?width=890)](https://opencollective.com/Radarr#sponsor)

## Mega Sponsors

[![Mega Sponsors List](https://opencollective.com/Radarr/tiers/mega-sponsor.svg?width=890)](https://opencollective.com/Radarr#mega-sponsor)

## JetBrains

Thank you to [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/jetbrains.png" alt="JetBrains" width="96">](http://www.jetbrains.com/) for providing us with free licenses to their great tools.

* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/ReSharper_icon.png" alt="ReSharper" width="32"> ReSharper](http://www.jetbrains.com/resharper/)
* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/WebStorm_icon.png" alt="WebStorm" width="32"> WebStorm](http://www.jetbrains.com/webstorm/)
* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/Rider_icon.png" alt="Rider" width="32"> Rider](http://www.jetbrains.com/rider/)
* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/dotTrace_icon.png" alt="dotTrace" width="32"> dotTrace](http://www.jetbrains.com/dottrace/)

## DigitalOcean

This project is also supported by DigitalOcean
<p>
  <a href="https://www.digitalocean.com/">
    <img src="https://opensource.nyc3.cdn.digitaloceanspaces.com/attribution/assets/SVG/DO_Logo_horizontal_blue.svg" width="201px">
  </a>
</p>

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2010-2025
