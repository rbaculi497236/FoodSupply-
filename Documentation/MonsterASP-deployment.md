# Deploy FoodSupply to MonsterASP

The database export and import (classmate guide steps 1–4) are already complete.
This repository uses `.github/workflows/build.yml` for both verification and deployment;
do not add a second deployment workflow. Pushes to `main` deploy after tests pass.
Pull requests and other branches only run verification. You can also use Actions →
Build, test and deploy to MonsterASP → Run workflow on `main`.

## Steps 5–6: Repository files

The ignore rules exclude downloaded publishing credentials and local production
settings. The existing tracked appsettings files retain application defaults;
never put hosted passwords in them. Ignoring a tracked file does not untrack it.
The deployment workflow clears the published database connection string and removes
environment-specific appsettings files. The server must supply its connection string.

## Steps 7–8: Website configuration

Open MonsterASP → Websites → Manage website. Use your existing website/domain.
Select ASP.NET Core / .NET 10 in its scripting settings. This is a .NET application;
Node.js is not needed. The publish is framework-dependent and requires .NET 10 on
the server. Enable HTTPS for the public site.

Open Scripting → Environment Variables and add separate key/value entries:

| Key | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Server=YOUR_MYSQL_HOST;Port=3306;Database=YOUR_DATABASE;User=YOUR_DATABASE_USER;Password=YOUR_DATABASE_PASSWORD;` |
| `Application__PublicUrl` | Your public HTTPS website URL |

Use the database connection details provided for your hosted website, not localhost
or your local root account. Use the actual port if different. Enter the connection
string as a single value without enclosing JSON or extra surrounding quotes.
Follow the provider's MySQL TLS settings if additional connection options are required.
Save the values and restart the website after changing them.

Password recovery additionally requires `Smtp__Host`, `Smtp__Port` (usually 587),
`Smtp__Username`, `Smtp__Password`, and `Smtp__From`; see Upgrade-and-operations.md.

## Steps 9–10: Web Deploy credentials

In MonsterASP → your website → Deploy, enable Web Deploy and open its login details.
In your GitHub repository → Settings → Secrets and variables → Actions →
New repository secret, create all four entries:

| GitHub secret name | Value from MonsterASP Web Deploy |
| --- | --- |
| `WEBSITE_NAME` | IIS site ID, such as `site12345`, not the public domain |
| `SERVER_COMPUTER_NAME` | Full publishing endpoint, such as `https://site12345.siteasp.net:8172/msdeploy.axd?site=site12345` |
| `SERVER_USERNAME` | Web Deploy username |
| `SERVER_PASSWORD` | Web Deploy password, not the MySQL password |

Use the actual endpoint supplied by MonsterASP. Keep these values in GitHub secrets.
The workflow does not print credentials or delete destination-only files. It uses
AppOffline while syncing the app, so deployment can briefly interrupt requests.

## Step 11: Push and check

Review and commit `.gitignore`, `.github/workflows/build.yml`, and this guide, then
push your `main` branch. Open GitHub Actions and inspect both `verify` and `deploy`.
After a successful deployment, open the public HTTPS URL and test login and a
read-only page with your imported account.

A successful upload does not prove database connectivity. If startup fails, inspect
MonsterASP application logs. Check the environment variable spelling, database
host/user/password, .NET 10 runtime, and database table-name case sensitivity.
If the app returns a database-upgrade-required 503, follow
`Documentation/Upgrade-and-operations.md`: stop the app, back up the hosted database,
and explicitly run the upgrade against that database. Deployments intentionally do
not run migrations or modify database records. An imported database that already
has all migrations does not need another upgrade merely because it was deployed.

## References

- https://help.monsterasp.net/books/development/page/environment-variables-as-configuration-store
- https://help.monsterasp.net/books/deploy/page/how-to-deploy-website-content-from-command-line
