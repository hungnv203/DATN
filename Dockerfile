FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy toàn bộ source code vào container
COPY . .

# Phục hồi các packages
RUN dotnet restore MovieBooking/MovieBookingAPI.csproj

# Build và publish project ra thư mục /app/publish
RUN dotnet publish MovieBooking/MovieBookingAPI.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Copy các file đã build từ bước trước
COPY --from=build /app/publish .

# Tên file dll dựa trên tên của file MovieBookingAPI.csproj
ENTRYPOINT ["dotnet", "MovieBookingAPI.dll"]
