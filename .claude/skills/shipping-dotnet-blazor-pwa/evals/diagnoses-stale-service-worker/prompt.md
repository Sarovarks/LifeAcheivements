---
tags: [skill-creator]
max_turns: 20
timeout_seconds: 900
allowed_tools: [Read, Glob, Grep, Skill, Write, Edit, Bash]
---

my blazor wasm app is hosted on cloudflare pages. i deployed a fix two days ago. works fine for me, works in incognito, but my friend still sees the old version on his phone - and he says sometimes it just fails to load completely, blank screen.

curl returns 200 every single time i check. i already added Cache-Control: no-store on index.html in _headers and it changed nothing.

whats actually going on here and how do i fix it properly? write it to diagnosis.md
