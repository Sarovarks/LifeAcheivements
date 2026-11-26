#!/bin/sh
# Blazor WebAssembly reads wwwroot/appsettings.json in the browser at startup, which means
# the API address can be decided here rather than baked in at publish time. One image then
# runs against local, staging or production by changing an environment variable.
set -e

CONFIG=/usr/share/nginx/html/appsettings.json

if [ -n "${ApiBaseUrl}" ]; then
    printf '{\n  "ApiBaseUrl": "%s"\n}\n' "${ApiBaseUrl}" > "${CONFIG}"
    echo "appsettings.json: ApiBaseUrl set to ${ApiBaseUrl}"
else
    echo "appsettings.json: ApiBaseUrl not set, leaving the published default in place"
fi
