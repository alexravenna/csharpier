FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-preview.5 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0.100-preview.5 AS build

RUN set -uex \
    && apt-get update \
    && apt-get install -y ca-certificates curl gnupg \
    && mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
     | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg \
    && NODE_MAJOR=18 \
    && echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_$NODE_MAJOR.x nodistro main" \
     | tee /etc/apt/sources.list.d/nodesource.list \
    && apt-get update \
    && apt-get install nodejs -y;

WORKDIR /build
COPY ./Directory.Build.props ./Directory.Packages.props ./
COPY ./Src/CSharpier.Playground/CSharpier.Playground.csproj Src/CSharpier.Playground/
COPY ./Src/CSharpier.Core/CSharpier.Core.csproj Src/CSharpier.Core/
RUN dotnet restore "Src/CSharpier.Playground/CSharpier.Playground.csproj"

COPY ./Src/CSharpier.Playground/ClientApp/package.json Src/CSharpier.Playground/ClientApp/
COPY ./Src/CSharpier.Playground/ClientApp/package-lock.json Src/CSharpier.Playground/ClientApp/
WORKDIR /build/Src/CSharpier.Playground/ClientApp
RUN npm ci
COPY ./Src/CSharpier.Playground/ClientApp/ .
RUN npm run build

WORKDIR /build
COPY . .

FROM build AS publish
RUN dotnet publish "Src/CSharpier.Playground/CSharpier.Playground.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CSharpier.Playground.dll"]