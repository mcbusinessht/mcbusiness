# Étape 1 : Construire l'application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copier les fichiers du projet
COPY *.csproj ./
RUN dotnet restore

# Copier tous les fichiers du projet
COPY . .

# Compiler l'application
RUN dotnet publish -c Release -o /out

# Étape 2 : Exécuter l'application
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# Exposer le port utilisé par l'application
EXPOSE 3000

# Commande pour démarrer l'application
ENTRYPOINT ["dotnet", "MCBusiness.dll"]
