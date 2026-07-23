FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG configuration=Release
WORKDIR /src
COPY . .
RUN dotnet restore "rubato.slnx"
RUN dotnet build "rubato.slnx" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "rubato.slnx" -c $configuration -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5161

ENV ASPNETCORE_URLS=http://+:5161
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=en_US.UTF-8
ENV LANG=en_US.UTF-8
ENV DataPath=/etc/rubato

VOLUME /etc/rubato

USER app

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Rubato.dll"]
