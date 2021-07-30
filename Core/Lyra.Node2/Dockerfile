#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["Core/Lyra.Node2/Noded.csproj", "Core/Lyra.Node2/"]
COPY ["Core/Lyra.Core/Core.csproj", "Core/Lyra.Core/"]
COPY ["Core/Lyra.Data/Lyra.Data.csproj", "Core/Lyra.Data/"]
COPY ["Core/Lyra.Shared/Shared.csproj", "Core/Lyra.Shared/"]
RUN dotnet restore "Core/Lyra.Node2/Noded.csproj"
COPY . .
WORKDIR "/src/Core/Lyra.Node2"
RUN dotnet build "Noded.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Noded.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV LYRA_POS_WALLET_NAME poswallet
ENV LYRA_POS_WALLET_PASSWORD VeryStrongP@ssW0rd
ENV LYRA_NETWORK testnet
ENV LYRA_DB_NAME lyra
ENV LYRA_DB_USER dbuser
ENV LYRA_DB_PASSWORD alongpassword
ENV LYRA_P2P_PORT 4505
ENV LYRA_API_PORT 4504

ENV ASPNETCORE_URLS http://*:${LYRA_P2P_PORT};https://*:${LYRA_API_PORT}
ENV ASPNETCORE_HTTPS_PORT ${LYRA_API_PORT}
ENV LYRA_ApplicationConfiguration__LyraNode__Lyra__Database__DBConnect=mongodb://${LYRA_DB_USER}:${LYRA_DB_PASSWORD}@127.0.0.1/${LYRA_DB_NAME}
ENV LYRA_ApplicationConfiguration__LyraNode__Lyra__Wallet__Name ${LYRA_POS_WALLET_NAME}
ENV LYRA_ApplicationConfiguration__LyraNode__Lyra__Wallet__Password ${LYRA_POS_WALLET_PASSWORD}

ENTRYPOINT ["dotnet", "lyra.noded.dll"]
