FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Scalable-System-Design-Implementation.sln ./
COPY src/ProductApi/ProductApi.csproj src/ProductApi/
RUN dotnet restore src/ProductApi/ProductApi.csproj

COPY src/ProductApi/ src/ProductApi/
RUN dotnet publish src/ProductApi/ProductApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProductApi.dll"]
