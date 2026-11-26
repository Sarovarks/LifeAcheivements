// Life Achievement Unlocker - Azure Container Apps + managed PostgreSQL.
//
//   az group create --name rg-lifeachievements --location centralindia
//   az deployment group create -g rg-lifeachievements -f infra/main.bicep \
//        -p dbAdminPassword='<a strong password>'
//
// Everything here is sized for a personal project. The notes on each resource say what to
// change when it stops being one.

@description('Short name used as the prefix for every resource.')
@minLength(3)
@maxLength(12)
param appName string = 'lifeach'

@description('Where to deploy. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('PostgreSQL administrator login.')
param dbAdminUser string = 'lifeadmin'

@description('PostgreSQL administrator password. Pass at deploy time; never commit it.')
@secure()
param dbAdminPassword string

@description('Container image for the API, including tag.')
param apiImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Container image for the web app, including tag.')
param webImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

var dbName = 'lifeachievements'
var suffix = uniqueString(resourceGroup().id)
var registryName = '${appName}acr${suffix}'

// ---------------------------------------------------------------- registry --

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    // Container Apps authenticates with a managed identity below, so the admin
    // user stays switched off.
    adminUserEnabled: false
  }
}

// ---------------------------------------------------------------- database --

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${appName}-pg-${suffix}'
  location: location
  sku: {
    // The cheapest burstable tier. Move to a General Purpose SKU before this
    // carries anybody's real data.
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: dbAdminUser
    administratorLoginPassword: dbAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: dbName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Container Apps egress IPs are not fixed, so this opens the server to Azure services.
// A VNet-integrated environment with a private endpoint is the real answer; this is the
// hobby-tier compromise, and the database still requires the password above.
resource allowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ------------------------------------------------------------ observability --

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-logs-${suffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ----------------------------------------------------------- container apps --

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${appName}-identity'
  location: location
}

// AcrPull, so the container apps can pull images without a stored password.
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, identity.id, 'AcrPull')
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${appName}-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  dependsOn: [acrPull]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        corsPolicy: {
          allowedOrigins: ['https://${appName}-web.${environment.properties.defaultDomain}']
          allowedMethods: ['*']
          allowedHeaders: ['*']
        }
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
      secrets: [
        {
          name: 'connection-string'
          value: 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=${dbName};Username=${dbAdminUser};Password=${dbAdminPassword};SSL Mode=Require;Trust Server Certificate=true'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__Default'
              secretRef: 'connection-string'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: 'https://${appName}-web.${environment.properties.defaultDomain}'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        // One replica minimum: the API migrates on startup, and several replicas
        // racing to migrate the same database is a bad time. Raise minReplicas only
        // after moving migrations into the deployment step.
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${appName}-web'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  dependsOn: [acrPull]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              // Written into appsettings.json when the container starts.
              name: 'ApiBaseUrl'
              value: 'https://${api.properties.configuration.ingress.fqdn}'
            }
          ]
        }
      ]
      scale: {
        // Static files behind nginx, so it can scale to nothing between visits.
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
}

// ----------------------------------------------------------------- outputs --

output registryLoginServer string = registry.properties.loginServer
output registryName string = registry.name
output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output webUrl string = 'https://${web.properties.configuration.ingress.fqdn}'
output postgresHost string = postgres.properties.fullyQualifiedDomainName
