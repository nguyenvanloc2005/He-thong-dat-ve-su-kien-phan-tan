FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore backend/TicketApi/TicketApi.csproj
RUN dotnet publish backend/TicketApi/TicketApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "TicketApi.dll"]
