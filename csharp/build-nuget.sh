#!/bin/bash

# EYWA C# Client - NuGet Build and Pack Script

echo "🚀 Building EYWA C# Client for NuGet..."

# Clean previous builds
echo "🧹 Cleaning previous builds..."
dotnet clean src/EywaClient/EywaClient.csproj --configuration Release

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore src/EywaClient/EywaClient.csproj

# Build for all target frameworks
echo "🔨 Building for multiple target frameworks..."
dotnet build src/EywaClient/EywaClient.csproj --configuration Release --no-restore

# Run tests (if any)
echo "🧪 Running tests..."
if [ -d "tests" ]; then
    dotnet test --configuration Release --no-build --verbosity normal
else
    echo "No tests found, skipping test execution."
fi

# Create NuGet package
echo "📦 Creating NuGet package..."
dotnet pack src/EywaClient/EywaClient.csproj --configuration Release --no-build --output ./nupkg

echo "✅ Build complete!"
echo ""
echo "📁 NuGet package created in: ./nupkg/"
echo "🚀 To publish to NuGet:"
echo "   dotnet nuget push ./nupkg/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json"
echo ""
echo "🔍 To test locally:"
echo "   dotnet add package EywaClient --source ./nupkg"
