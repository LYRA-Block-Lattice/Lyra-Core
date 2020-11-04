#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
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
ENTRYPOINT ["dotnet", "lyra.noded.dll"]