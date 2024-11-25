FROM mcr.microsoft.com/dotnet/sdk:8.0.100-1-alpine3.18-amd64 AS builder

# semantic version; major.minor.patch
ARG VERSION_PREFIX

# rc, alpha, etc.
ARG VERSION_SUFFIX=''

ARG CSPROJ='???'
ARG RID='linux-musl-x64'

WORKDIR /build

# .deps are restore relevant files prepared by dockerize_app.sh
COPY ./deps ./
RUN dotnet restore $CSPROJ -r $RID --packages ./nupkg --no-cache --nologo -v minimal

COPY . ./
RUN dotnet publish $CSPROJ -c Release --no-restore --packages ./nupkg --nologo -v minimal \
    -o /publish \
    -r $RID \
    --self-contained \
    -p:PublishTrimmed=true \
    -p:PublishSingleFile=true \
    -p:RunAnalyzers=false \
    -p:AnalysisMode=None \
    -clp:NoSummary \
    -p:VersionPrefix="$VERSION_PREFIX" \
    -p:VersionSuffix="$VERSION_SUFFIX"


FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0.0-alpine3.18-amd64
ARG APP_DIR=/app

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    TZ=UTC

RUN apk update \
  && apk upgrade --no-cache --timeout 30 --quiet \
  && apk add --no-cache --timeout 30 --quiet tzdata dumb-init \
  && rm -rf /var/cache/apk/* \
  && cp -f /usr/share/zoneinfo/$TZ /etc/localtime \
  && echo $TZ > /etc/timezone \
  && addgroup -S dotnet && adduser -S dotnet -G dotnet \
  && mkdir $APP_DIR && chown dotnet $APP_DIR

WORKDIR $APP_DIR
COPY --chown=dotnet:dotnet --from=builder /publish ./

USER dotnet

ENTRYPOINT ["/usr/bin/dumb-init", "--"]
CMD ./???