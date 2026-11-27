# Skills

`shipping-dotnet-blazor-pwa` is the reusable half of this project: the stack, the deployment
setup and the verification habits, with the app's subject matter left out.

This copy is the versioned one. The copy Claude actually loads lives at
`~/.claude/skills/shipping-dotnet-blazor-pwa`, so it is available in every project rather than
only this one. The two can drift; re-sync with:

    cp -r .claude/skills/shipping-dotnet-blazor-pwa ~/.claude/skills/

`evals/` holds three test cases and their assertions. Results from the run on 27 November 2026:
94% with the skill against 83% without, at one run per case — suggestive, not conclusive, and
15 of the 18 assertions passed either way. The two that did not are worth knowing: without the
skill the API address was hardcoded into a committed `appsettings.json`, which produces a site
that loads perfectly and silently cannot sign anyone in.

To run them again with Claude Code:

    claude plugin eval .claude/skills/shipping-dotnet-blazor-pwa --allow-tools Write,Edit,Bash

Those are real billed model runs.
