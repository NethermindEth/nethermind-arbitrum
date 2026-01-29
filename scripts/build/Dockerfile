# SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

FROM mcr.microsoft.com/dotnet/sdk:10.0.100-noble

ARG COMMIT_HASH
ARG SOURCE_DATE_EPOCH

ENV CI=true

WORKDIR /nethermind

COPY src/Nethermind src/Nethermind
COPY src/Nethermind.Arbitrum src/Nethermind.Arbitrum
COPY src/Directory.Build.props src/
COPY src/nuget.config src/
COPY --chmod=0755 scripts/build/build.sh .

RUN ./build.sh $COMMIT_HASH

VOLUME /output

CMD ["cp", "-r", "output/.", "/output"]
