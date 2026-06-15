FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

USER root
RUN apt-get update && apt-get install -y libgssapi-krb5-2
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["be_digitalin/be_digitalin.csproj", "be_digitalin/"]
RUN dotnet restore "be_digitalin/be_digitalin.csproj"
COPY . .
WORKDIR "/src/be_digitalin"
RUN dotnet build "./be_digitalin.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./be_digitalin.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "be_digitalin.dll"]
