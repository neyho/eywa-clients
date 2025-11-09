# EYWA C# Client - NuGet Build and Pack Script (PowerShell)

Write-Host "🚀 Building EYWA C# Client for NuGet..." -ForegroundColor Green

# Clean previous builds
Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean src/EywaClient/EywaClient.csproj --configuration Release

# Restore dependencies
Write-Host "📦 Restoring dependencies..." -ForegroundColor Yellow
dotnet restore src/EywaClient/EywaClient.csproj

# Build for all target frameworks
Write-Host "🔨 Building for multiple target frameworks..." -ForegroundColor Yellow
dotnet build src/EywaClient/EywaClient.csproj --configuration Release --no-restore

# Run tests (if any)
Write-Host "🧪 Running tests..." -ForegroundColor Yellow
if (Test-Path "tests") {
    dotnet test --configuration Release --no-build --verbosity normal
} else {
    Write-Host "No tests found, skipping test execution." -ForegroundColor Gray
}

# Create NuGet package
Write-Host "📦 Creating NuGet package..." -ForegroundColor Yellow
dotnet pack src/EywaClient/EywaClient.csproj --configuration Release --no-build --output ./nupkg

Write-Host "✅ Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📁 NuGet package created in: ./nupkg/" -ForegroundColor Cyan
Write-Host "🚀 To publish to NuGet:" -ForegroundColor Cyan
Write-Host "   dotnet nuget push ./nupkg/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json" -ForegroundColor White
Write-Host ""
Write-Host "🔍 To test locally:" -ForegroundColor Cyan
Write-Host "   dotnet add package EywaClient --source ./nupkg" -ForegroundColor White
