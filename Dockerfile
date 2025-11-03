# Dockerfile para Render - Apenas API Web
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Criar projeto da API separado
COPY ["OBDiiApiWeb/OBDiiApiWeb.csproj", "OBDiiApiWeb/"]
RUN dotnet restore "OBDiiApiWeb/OBDiiApiWeb.csproj"

COPY . .
WORKDIR "/src/OBDiiApiWeb"
RUN dotnet build "OBDiiApiWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OBDiiApiWeb.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copiar arquivo de ambiente
COPY .env

# Definir variável de ambiente para a porta
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "OBDiiApiWeb.dll"]