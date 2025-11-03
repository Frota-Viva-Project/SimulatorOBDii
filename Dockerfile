# Dockerfile para Render - OBD-II API Web
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

# Instalar dependências do sistema para comunicação serial (se necessário)
RUN apt-get update && apt-get install -y \
    udev \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar arquivos de projeto primeiro para melhor cache de layers
COPY ["OBDiiApiWeb/OBDiiApiWeb.csproj", "OBDiiApiWeb/"]
RUN dotnet restore "OBDiiApiWeb/OBDiiApiWeb.csproj"

# Copiar código fonte
COPY . .
WORKDIR "/src/OBDiiApiWeb"
RUN dotnet build "OBDiiApiWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OBDiiApiWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copiar arquivo de ambiente se existir
COPY .env* ./

# Criar usuário não-root para segurança
RUN addgroup --system --gid 1001 dotnet \
    && adduser --system --uid 1001 --ingroup dotnet dotnet

# Dar permissões necessárias
RUN chown -R dotnet:dotnet /app
USER dotnet

# Configurar variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "OBDiiApiWeb.dll"]