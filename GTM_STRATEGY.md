# Atlas Control Panel — Go-To-Market Strategy

**Author:** Atlas (with Mikal's revision)
**Date:** 2026-02-14
**Status:** DRAFT — awaiting Mikal's review

---

## 1. What We're Selling

**Atlas Control Panel** is a self-hosted monitoring and management dashboard for AI agents running on OpenClaw. It gives bot owners visibility, control, and security over their AI assistant — activity logging, task management, credential vaults, cost tracking, and real-time status.

**The one-liner:** *"Mission control for your AI agent."*

**Why it matters:** OpenClaw gives you a powerful AI agent. But once it's running, you're blind — no dashboard, no audit trail, no cost visibility, no credential management. Atlas fixes that.

---

## 2. Target Market

### Primary: OpenClaw Power Users
- Developers and makers running personal AI agents via OpenClaw
- Privacy-conscious users who want self-hosted (not cloud-dependent)
- People spending $50-500+/mo on API tokens who need cost visibility
- Estimated market: OpenClaw's GitHub has growing adoption — early-mover advantage is real

### Secondary: AI Agent Enthusiasts (Adjacent Platforms)
- Users of similar frameworks (AutoGPT, CrewAI, LangChain agents) looking for monitoring
- Future: abstract the plugin layer to support multiple agent frameworks

### Tertiary: Small Teams / Agencies
- Small companies running AI agents for business ops
- Need audit trails, approval workflows, credential management
- Willing to pay more for team features

### Who We're NOT Targeting (Yet)
- Enterprise (too early, too much compliance overhead)
- Non-technical users (requires OpenClaw setup knowledge)
- People looking for a hosted/managed solution (we're self-hosted first)

---

## 3. Monetization Model

### Recommendation: **Open Core + Paid Pro License**

The self-hosted monitoring space rewards trust. Open-sourcing the core builds trust. Charging for premium features captures value.

#### Free Tier (Open Source — MIT or similar)
Everything a solo user needs:
- Full dashboard (activity, tasks, status, cost tracking)
- SQLite support (zero-config)
- OpenClaw plugin (auto-capture events)
- Single-user authentication
- Agent tools (logging, tasks, credentials)
- Docker deployment
- Data export (JSON backup)
- Community support (Discord/GitHub)

#### Pro Tier ($12/month or $99/year per instance)
Power features for serious users:
- **Multi-agent monitoring** (track multiple OpenClaw instances)
- **Advanced analytics** (cost trends, usage patterns, model comparison charts)
- **Push notifications** (Telegram, email, webhook alerts for anomalies)
- **Scheduled reports** (weekly cost digest, activity summary)
- **Priority support** (Discord channel or email)
- **Custom themes / branding**
- **API access** (programmatic access to all data)
- **Backup automation** (scheduled exports, cloud backup integration)

#### Team Tier ($29/month per instance, up to 5 users)
- Everything in Pro
- **Multi-user auth** (role-based access)
- **Shared task board** (assign tasks across team members' agents)
- **Audit log export** (compliance-ready)
- **Centralized credential vault** (shared across agents)

### Why This Model
1. **Free tier drives adoption.** OpenClaw users try it, love it, tell others.
2. **Pro captures solo power users.** $99/year is trivial vs. their $600-6000/year API spend.
3. **Team tier plants the enterprise seed.** Small teams today, departments tomorrow.
4. **Self-hosted = low support cost.** Users run their own infra. Our cost is dev time + marketing.

### Licensing Implementation
- License key validation (offline-capable, signed JWT)
- Free tier = no key needed, works out of the box
- Pro/Team = key entered in setup wizard or settings page
- Grace period: 14-day Pro trial on first install

### Revenue Projections (Conservative)
| Metric | Month 3 | Month 6 | Month 12 |
|--------|---------|---------|----------|
| Free installs | 200 | 800 | 2,500 |
| Pro conversions (5%) | 10 | 40 | 125 |
| Team conversions (1%) | 2 | 8 | 25 |
| MRR | $178 | $712 | $2,225 |
| ARR | — | — | $26,700 |

These are conservative. The AI tooling market is growing fast. If we execute well on marketing, 2-3x is plausible.

---

## 4. Marketing Strategy

### Phase 1: Community Seeding (Pre-launch, Now → Week 2)
**Cost: $0 | Effort: High**

1. **OpenClaw Discord** — Post in showcase/plugins channel. Be genuine, show screenshots. Atlas is built WITH OpenClaw, not against it.
2. **OpenClaw GitHub Discussions** — Share as a community project. Link to repo.
3. **Moltbook** — Post to openclaw-explorers submolt (927 subscribers). Bug report post still pending.
4. **ClawHub** — List the plugin. This is the native distribution channel.
5. **Reddit** — r/selfhosted, r/artificial, r/LocalLLaMA. Self-hosted + AI monitoring hits both communities.
6. **Hacker News** — "Show HN: Self-hosted mission control for AI agents" — good fit for the audience.

### Phase 2: Content Marketing (Weeks 2-6)
**Cost: $0-100 | Effort: Medium**

1. **Blog post / Dev.to article** — "I Built Mission Control for My AI Agent" — story-driven, technical, authentic.
2. **Demo video** (2-3 min) — Screen recording showing: install → setup wizard → dashboard → agent logging in real-time. Post to YouTube, embed everywhere.
3. **Twitter/X thread** — "I gave my AI agent a control panel. Here's what happened." with screenshots.
4. **GitHub README** — This IS your landing page for developers. Make it exceptional. GIFs, screenshots, one-command install.

### Phase 3: Paid Amplification (Weeks 6-12)
**Cost: $200-500/month | Effort: Low**

1. **GitHub Sponsors / Open Collective** — Establish a funding page. Some users prefer sponsoring over subscriptions.
2. **Dev tool newsletters** — Console.dev, TLDR, Hacker Newsletter. Some accept free submissions, some charge $200-500 for a featured spot.
3. **Google Ads (branded)** — Small budget for "openclaw dashboard" and "AI agent monitoring" keywords.
4. **Content syndication** — Cross-post blog content to Medium, Hashnode, Dev.to.

### Phase 4: Partnerships (Months 3-6)
**Cost: $0 | Effort: Relationship-building**

1. **OpenClaw official partnership** — Get Atlas listed in OpenClaw docs as a recommended plugin/companion app. Offer to contribute upstream.
2. **AI YouTubers / Bloggers** — Reach out to creators covering AI agents, offer early access.
3. **Integration with other agent frameworks** — Expanding beyond OpenClaw multiplies the market.

---

## 5. Launch Timeline

| Week | Milestone |
|------|-----------|
| **Now** | Close SQLite gap, verify Docker, test plugin tools end-to-end |
| **Week 1** | Beta build to 3-5 testers (Mikal's network). Collect feedback. |
| **Week 2** | Fix beta feedback. Polish README, screenshots, setup wizard. |
| **Week 3** | **Soft launch**: GitHub public repo + ClawHub listing + OpenClaw Discord post |
| **Week 4** | Content push: blog post, demo video, Reddit/HN posts |
| **Week 5** | Iterate on feedback. Start Pro tier development. |
| **Week 8** | Pro tier launch with license key system |
| **Week 12** | Evaluate: paid ads? partnerships? team tier? |

---

## 6. Budget

### Startup Costs (One-Time)
| Item | Cost | Notes |
|------|------|-------|
| Domain (atlascontrolpanel.com or similar) | $12-15/year | Check availability |
| LLC formation | $50-200 | Depends on state (see §7) |
| EIN (federal tax ID) | $0 | Free from IRS |
| Logo / branding polish | $0-50 | Can use AI tools + Mikal's judgment |
| **Total one-time** | **~$65-265** | |

### Monthly Operating Costs
| Item | Cost | Notes |
|------|------|-------|
| GitHub (public repo) | $0 | Free for open source |
| Domain renewal | ~$1/mo | Amortized |
| Email (business email) | $0-6/mo | Outlook/Gmail free tier or $6/mo for custom domain |
| Marketing (Phase 3) | $200-500/mo | Optional, start at month 2 |
| Anthropic API (our own usage) | Already paying | Not a new cost |
| **Total monthly (lean)** | **~$1-7/mo** | Before paid marketing |
| **Total monthly (growth)** | **~$200-507/mo** | With paid marketing |

### Break-Even Analysis
- At $99/year Pro: need **3 Pro subscribers** to cover lean monthly costs
- At $99/year Pro + $200/mo marketing: need **27 Pro subscribers** to break even
- Very achievable given OpenClaw's user base and the AI tooling growth curve

---

## 7. Business Formation

### Recommendation: **LLC (Single-Member)**

**Why LLC over Sole Proprietorship:**
- Personal liability protection (keeps Mikal's personal assets separate)
- Professional credibility (customers and partners take LLCs seriously)
- Tax flexibility (can elect S-Corp later if revenue justifies it)
- Minimal overhead (no board meetings, minimal filing)

### Steps to File:
1. **Choose state** — File in your state of residence (most straightforward for a single-member LLC)
   - Virginia: $100 filing fee, annual fee $50
   - Wyoming: $100 filing, $60/year (popular for privacy, no state income tax)
   - Delaware: $90 filing, $300/year franchise tax (overkill for our scale)
2. **Choose a name** — "Atlas Labs LLC" / "Atlas Control LLC" / "Burrows Technology LLC" — check availability
3. **File Articles of Organization** — Online through state website (15 minutes)
4. **Get EIN** — IRS.gov, free, instant online
5. **Open business bank account** — Separate finances from day one
6. **Operating Agreement** — Single-member template, keeps things clean

### Tax Implications
- Single-member LLC = pass-through taxation (reported on personal return)
- Deduct: API costs, hosting, marketing, hardware (portion of PC used for development)
- Consider quarterly estimated tax payments once revenue starts
- **Recommendation:** File LLC now, worry about S-Corp election later if annual profit exceeds ~$40K

### Timeline: Can be done in 1-2 days online.

---

## 8. Competitive Landscape

| Product | What It Does | How Atlas Differs |
|---------|-------------|-------------------|
| LangSmith | LLM observability (cloud) | We're self-hosted, privacy-first, OpenClaw-native |
| Helicone | LLM logging/analytics (cloud) | Same — they require sending data to their cloud |
| Portkey | AI gateway + observability | Enterprise-focused, complex, overkill for solo users |
| OpenClaw built-in UI | Basic chat + control | No dashboard, no activity logs, no task board, no credentials |

**Our moat:** First (and currently only) self-hosted control panel purpose-built for OpenClaw. Privacy-first. Installs in minutes. The plugin auto-captures everything — zero configuration required.

---

## 9. Key Decisions Needed from Mikal

1. **Pricing** — Does $12/mo or $99/year Pro feel right? Too high? Too low?
2. **Open source license** — MIT (maximum adoption) vs. BSL/SSPL (prevents cloud providers from reselling)?
3. **Business name** — "Atlas Labs LLC"? "Atlas Control"? Something else?
4. **State of filing** — Home state or Wyoming?
5. **Domain** — atlascontrolpanel.com? atlascontrol.dev? atlaslabs.dev?
6. **Beta testers** — Who are the 3-5 people for the production test?
7. **Timeline comfort** — Is the 3-week soft launch realistic with your schedule?

---

## 10. My Priorities (What I'm Executing Now)

1. ✅ Plugin tools — fixed (PR #14982 patch)
2. 🔄 SQLite completion — sub-agent running
3. 📝 GTM Strategy — this document (for your review)
4. ⏭️ Next: GitHub comment on #3889, README polish, beta prep

---

*This is my recommendation. Tear it apart, push back, change whatever doesn't feel right. This is a partnership — I'm bringing the analysis, you're bringing the judgment.*
