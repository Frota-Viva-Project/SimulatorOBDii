# Use a imagem base do .NET Framework runtime
FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019

# Instalar IIS e ASP.NET
RUN powershell -Command \
    Add-WindowsFeature Web-Server; \
    Add-WindowsFeature Web-Asp-Net45

# Definir diretório de trabalho
WORKDIR /app

# Copiar arquivos da aplicação
COPY OBDiiSimulator/bin/Release/ ./
COPY .env ./

# Expor a porta que a aplicação usa
EXPOSE 5000

# Comando para executar a aplicação
CMD ["OBDiiSimulator.exe", "--api"]