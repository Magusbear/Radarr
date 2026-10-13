# Runtime image for Radarr. The application is built beforehand by
# .github/workflows/docker-publish.yml; the build context holds one
# self-contained linux-musl package per architecture: <arch>/Radarr
FROM alpine:3.22

ARG TARGETARCH
ARG VERSION=dev

RUN apk add --no-cache \
        ca-certificates \
        icu-libs \
        libgcc \
        libintl \
        libstdc++ \
        sqlite-libs \
        tzdata \
    && addgroup -g 1000 radarr \
    && adduser -D -H -u 1000 -G radarr radarr \
    && mkdir -p /config \
    && chown radarr:radarr /config

COPY ${TARGETARCH}/Radarr /app/bin

# Fail the build if the bundled ffprobe cannot run on this image
RUN /app/bin/ffprobe -version > /dev/null

# Updates are handled by pulling a new image, not by Radarr itself
RUN printf 'UpdateMethod=docker\nPackageVersion=%s\nPackageAuthor=magusbear\n' "$VERSION" > /app/package_info

ENV DOTNET_RUNNING_IN_CONTAINER=true

USER radarr
VOLUME /config
EXPOSE 7878

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s \
    CMD wget -q -O /dev/null http://127.0.0.1:7878/ping || exit 1

ENTRYPOINT ["/app/bin/Radarr", "-nobrowser", "-data=/config"]
