# Deployment

Phase 3: containers, CI, and the Azure infrastructure to run this somewhere real.

Everything here has been built and run locally. **Nothing has been deployed to Azure** - that
spends money in your subscription, so the commands are yours to run.

---

## Running the whole thing in containers

The same images that get deployed, on your machine:

```powershell
docker compose -f docker-compose.full.yml up -d --build
```

| | URL |
|---|---|
| Web | http://localhost:8080 |
| API | http://localhost:8081 |
| Health | http://localhost:8081/health/ready |

It reads the same `.env` as the development compose file. To stop it:

```powershell
docker compose -f docker-compose.full.yml down
```

Add `-v` to that if you also want the database volume gone.

### How the web container knows where the API is

Blazor WebAssembly runs in the browser, so anything baked in at publish time is fixed for the
life of the image. Instead, `docker-entrypoint.sh` writes `wwwroot/appsettings.json` when the
container starts, from the `ApiBaseUrl` environment variable:

```json
{ "ApiBaseUrl": "https://lifeach-api.whatever.azurecontainerapps.io" }
```

One image therefore runs against local, staging or production without rebuilding. nginx is told
never to cache that file, so a restart with a new value takes effect immediately.

### Health endpoints

| Path | Checks | Used for |
|---|---|---|
| `/health/live` | Process is up. **Does not touch the database.** | Liveness probe |
| `/health/ready` | Database reachable too | Readiness probe |

They are split on purpose: a database blip should take the app out of rotation, not get the
container killed and restarted in a loop.

---

## CI

`.github/workflows/ci.yml` runs on every push and pull request to `main`:

1. Restore, build, run all 139 tests, upload the results.
2. Build both Docker images to prove the Dockerfiles still work - built, not pushed, so a pull
   request from a fork needs no registry credentials.

Nothing to configure; it works as soon as the repository is on GitHub.

---

## Azure

`infra/main.bicep` describes the whole environment:

| Resource | SKU | Note |
|---|---|---|
| Container Registry | Basic | Admin user off; the apps pull with a managed identity |
| PostgreSQL Flexible Server | Standard_B1ms, 32 GB | Cheapest burstable tier |
| Log Analytics workspace | PerGB2018, 30 days | Container logs |
| Container Apps environment | Consumption | |
| API container app | 0.5 CPU / 1 GiB, 1-3 replicas | Liveness + readiness probes |
| Web container app | 0.25 CPU / 0.5 GiB, 0-3 replicas | Scales to zero between visits |

### 1. Create the infrastructure

```powershell
az login
```

```powershell
az group create --name rg-lifeachievements --location centralindia
```

```powershell
az deployment group create -g rg-lifeachievements -f infra/main.bicep -p dbAdminPassword='<a strong password>'
```

That first run starts both apps on a Microsoft placeholder image, because the registry is empty
until you push to it. The deploy workflow replaces them.

Keep the password in a password manager. It ends up as a Container Apps secret, not in the
repository.

### 2. Let GitHub Actions deploy without storing a secret

Create an app registration and give it federated access to this repository, so there is no
long-lived credential in GitHub:

```powershell
az ad app create --display-name lifeachievements-deploy
```

```powershell
az ad sp create --id <appId from the previous command>
```

```powershell
az role assignment create --assignee <appId> --role Contributor --scope /subscriptions/<subscriptionId>/resourceGroups/rg-lifeachievements
```

Then add a federated credential for the `main` branch (Entra ID portal → the app → Certificates
& secrets → Federated credentials → GitHub Actions), using:

- Organization: `Sarovarks`
- Repository: `LifeAchievements`
- Entity: Branch, `main`

Finally set three **repository variables** (not secrets - none of these is sensitive):

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | the app registration's appId |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |

### 3. Deploy

GitHub → Actions → **Deploy to Azure** → Run workflow.

It builds and pushes both images, updates the API, **waits for `/health/ready` to come back
200**, and only then points the web app at the new build. If the API never becomes healthy the
run fails before the frontend is touched.

Once a manual run has worked, uncomment the `push` trigger in `deploy.yml` to ship on every
merge to `main`.

### Deploying by hand instead

```powershell
az acr build -r <registryName> -t lifeachievements-api:latest -f src/LifeAchievements.Api/Dockerfile .
```

```powershell
az containerapp update -n lifeach-api -g rg-lifeachievements --image <registry>.azurecr.io/lifeachievements-api:latest
```

---

## Things worth knowing before this carries real data

These are deliberate hobby-tier compromises, not oversights:

- **The database firewall allows all Azure services** (`0.0.0.0`), because Container Apps egress
  IPs are not fixed. The real answer is a VNet-integrated environment with a private endpoint.
  The server still requires the password.
- **The API migrates on startup**, which is why `minReplicas` is 1 rather than 0 - several
  replicas racing to migrate the same database is a bad time. Set
  `RunMigrationsOnStartup=false` and apply migrations as a deployment step before scaling out.
- **`Trust Server Certificate=true`** is in the connection string. Fine against Azure's managed
  Postgres; do not carry that habit to a server you do not control.
- **No custom domain or CDN.** Both apps run on their generated `azurecontainerapps.io` names.
- **Burstable database, no high availability, 7-day backups.** Move to General Purpose before
  anyone else's data lives in it.
- **Cost**: the burstable database is the bulk of it, and it bills whether or not anyone visits.
  The web app scales to zero; the API does not, for the migration reason above. Delete the whole
  thing with `az group delete --name rg-lifeachievements` when you are done experimenting.
