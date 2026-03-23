FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY InvoicesProjectEntities/InvoicesProjectEntities.csproj InvoicesProjectEntities/
COPY InvoicesProjectApplication/InvoicesProjectApplication.csproj InvoicesProjectApplication/
COPY InvoicesProjectInfra/InvoicesProjectInfra.csproj InvoicesProjectInfra/
COPY InvoicesProjectAPI/InvoicesProjectAPI.csproj InvoicesProjectAPI/

RUN dotnet restore InvoicesProjectAPI/InvoicesProjectAPI.csproj

COPY . .

RUN dotnet publish InvoicesProjectAPI/InvoicesProjectAPI.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

ENTRYPOINT ["dotnet", "InvoicesProjectAPI.dll"]
