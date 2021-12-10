#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Client/Lyra.Client.CLI/Client.CLI.csproj", "Client/Lyra.Client.CLI/"]
COPY ["Core/Lyra.Core/Core.csproj", "Core/Lyra.Core/"]
COPY ["Core/Lyra.Data/Lyra.Data.csproj", "Core/Lyra.Data/"]
RUN dotnet restore "Client/Lyra.Client.CLI/Client.CLI.csproj"
COPY . .
WORKDIR "/src/Client/Lyra.Client.CLI"
RUN dotnet build "Client.CLI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Client.CLI.csproj" -c Release -o /app/publish

FROM base AS final

ENV LYRA_POS_WALLET_NAME poswallet
ENV LYRA_POS_WALLET_PASSWORD VeryStrongP@ssW0rd
ENV LYRA_NETWORK testnet

WORKDIR /app
COPY --from=publish /app/publish .
# Build a shell script because the ENTRYPOINT command doesn't like using ENV
# RUN echo "#!/bin/bash \n dotnet lyra.dll -n ${LYRA_NETWORK} -g ${LYRA_POS_WALLET_NAME} --password ${LYRA_POS_WALLET_PASSWORD}" > ./entrypoint.sh
# RUN chmod +x ./entrypoint.sh
# ENTRYPOINT ["./entrypoint.sh"]
ENTRYPOINT ["/bin/sh", "-c", "dotnet lyra.dll -n ${LYRA_NETWORK}"]