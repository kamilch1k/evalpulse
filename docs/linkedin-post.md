# LinkedIn Post Draft

I built EvalPulse, a .NET backend for AI evaluation and observability.

Instead of just wrapping an LLM API, it focuses on the part teams need before shipping AI features: measuring whether a model or prompt change got better, worse, slower, more expensive, or less safe.

What it does:

- scores model outputs against a golden dataset
- combines lexical F1, required-term coverage, and forbidden-term checks
- tracks latency, token count, and cost budgets
- compares candidate runs against a baseline
- returns regression gate failures as API responses suitable for CI

It uses synthetic fixtures only, so it runs without provider credentials.

Repo: https://github.com/kamilch1k/evalpulse
