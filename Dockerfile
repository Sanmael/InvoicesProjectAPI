FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY InvoicesProjectEntities/InvoicesProjectEntities.csproj InvoicesProjectEntities/
COPY InvoicesProjectApplication/InvoicesProjectApplication.csproj InvoicesProjectApplication/
COPY InvoicesProjectInfra/InvoicesProjectInfra.csproj InvoicesProjectInfra/
COPY InvoicesProjectAPI/InvoicesProjectAPI.csproj InvoicesProjectAPI/

RUN dotnet restore InvoicesProjectAPI/InvoicesProjectAPI.csproj

COPY . .

RUN dotnet publish InvoicesProjectAPI/InvoicesProjectAPI.csproj -c Release -o /app/out

# Build self-contained EF migration bundle for linux-x64
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project InvoicesProjectInfra/InvoicesProjectInfra.csproj \
    --startup-project InvoicesProjectAPI/InvoicesProjectAPI.csproj \
    --output /app/efbundle \
    --self-contained \
    -r linux-x64 \
    --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
COPY --from=build /app/efbundle ./efbundle
COPY entrypoint.sh ./entrypoint.sh
RUN chmod +x ./efbundle ./entrypoint.sh

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

ENTRYPOINT ["./entrypoint.sh"]
