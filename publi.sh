#!/bin/bash

rm -rf package
mkdir package
dotnet pack -p:MultiFramework=true -o package
#nuget add -Source ../packages package/*.nupkg
