FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["flare-api.slnx", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/Flare.Domain/Flare.Domain.csproj", "src/Flare.Domain/"]
COPY ["src/Flare.Application/Flare.Application.csproj", "src/Flare.Application/"]
COPY ["src/Flare.Infrastructure/Flare.Infrastructure.csproj", "src/Flare.Infrastructure/"]
COPY ["src/Flare.Api/Flare.Api.csproj", "src/Flare.Api/"]

RUN dotnet restore "flare-api.slnx"

COPY . .

WORKDIR "/src/src/Flare.Api"
RUN dotnet build "Flare.Api.csproj" -c Release -o /app/build


FROM build AS publish
RUN dotnet publish "Flare.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 5000

ENTRYPOINT ["dotnet", "Flare.Api.dll"]
