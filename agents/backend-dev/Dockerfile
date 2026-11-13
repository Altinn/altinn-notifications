# The backend developer agent's image for altinn-notifications. One image with a stage per agent; there is only
# `minimal`, since nothing in the work needs a browser or a desktop. The base is the agentctl release this definition
# is written for, pinned by digest: images/agentctl-base/README.md in digdir/digdir-agents describes it.
FROM ghcr.io/digdir/agentctl-base:ubuntu-26.04-v0.1.0-preview.14@sha256:4de3acc36600c2fba8e7e73c0de0ca812cbd32ae2286f1d7750f5394d5e90e72 AS minimal

# The base ends as the agent user; installing needs root.
USER root

# What .NET needs beside the base: ICU, without which every .NET program aborts, and zlib. psql for
# tools/dev-setup/dbsetup.sh and for looking at the database the tests and the API use.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libicu78 postgresql-client zlib1g \
    && rm -rf /var/lib/apt/lists/* \
    && psql --version

# The newest .NET 10 SDK when the image is built, rather than the patch global.json names: the repository is cloned at
# boot, after the build, and Renovate moves global.json often. Its rollForward (latestFeature) accepts a newer SDK, and
# the workflow rebuilds the image every week to stay ahead of it. dotnet-install.sh does not check what it downloads,
# so the archive it keeps is checked against the SHA-512 Microsoft publishes for that version.
ENV DOTNET_ROOT=/usr/share/dotnet
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
RUN set -eu \
    && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "${DOTNET_ROOT}" --no-path \
        --keep-zip --zip-path /tmp/dotnet-sdk.tar.gz \
    && version="$("${DOTNET_ROOT}/dotnet" --list-sdks | awk 'NR == 1 { print $1 }')" \
    && case "$(uname -m)" in x86_64) rid=linux-x64 ;; aarch64) rid=linux-arm64 ;; *) exit 1 ;; esac \
    && expected="$(curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json \
        | jq -r --arg v "${version}" --arg rid "${rid}" \
            '[.releases[].sdks[]? | select(.version == $v) | .files[] | select(.rid == $rid and (.name | endswith(".tar.gz"))) | .hash][0]')" \
    && echo "SDK ${version} (${rid}), expecting SHA-512 ${expected}" \
    && echo "${expected}  /tmp/dotnet-sdk.tar.gz" | sha512sum -c - \
    && ln -s "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet \
    && rm -f /tmp/dotnet-install.sh /tmp/dotnet-sdk.tar.gz \
    && dotnet --info

# The Aspire CLI, at the version tools/aspire/apphost.cs takes its SDK at, for `aspire run` of the whole stack.
# Installed outside /home/agent, with HOME, NuGet's caches and Aspire's logs in /tmp for the step, since what root
# leaves in /home/agent locks the agent out of it. The package's files are only for their owner, root, until chmod.
ARG ASPIRE_CLI_VERSION=13.6.5
RUN set -eu \
    && export HOME=/tmp/aspire-home DOTNET_CLI_HOME=/tmp/aspire-home NUGET_PACKAGES=/tmp/nuget-packages \
    && dotnet tool install Aspire.Cli --version "${ASPIRE_CLI_VERSION}" --tool-path /usr/local/lib/aspire-cli \
    && chmod -R a+rX /usr/local/lib/aspire-cli \
    && ln -s /usr/local/lib/aspire-cli/aspire /usr/local/bin/aspire \
    && aspire --version \
    && rm -rf /tmp/aspire-home /tmp/nuget-packages

# Podman with `docker` and `docker compose`, for Testcontainers in the *.IntegrationTestsASB projects (PostgreSQL,
# SQL Server and the Service Bus emulator) and for tools/asb-emulator.
RUN agent-feature install containers

# agentctl and agentd of the same release, so the agent can try a change to its own definition in a nested agent.
COPY --from=ghcr.io/digdir/agentctl:v0.1.0-preview.14@sha256:8f2334590d1363c1ae2041cf1e7983a2c81aa57f0d4956f2a27e97a0a3536a7d /agentctl /agentd /usr/local/bin/

# `dotnet tool install --global` puts tools here for the agent user.
ENV PATH=/home/agent/.dotnet/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin

COPY common/workspace.list /etc/agent-workspace.d/altinn-notifications.list
COPY common/90-inotify.conf /etc/sysctl.d/90-inotify.conf
COPY --chmod=0755 common/notifications-db /usr/local/bin/notifications-db

RUN agent-image-check

USER agent
