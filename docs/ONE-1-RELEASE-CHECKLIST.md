# ONE-1 release gate

**Current status: NOT RELEASED — no ONE-1 weights are included in this repository.**

Do not mark ONE-1 as released or call a third-party model an original ONE model just because it uses the ALLINONE provider interface. This checklist records the evidence required before the status can change.

- [ ] A legally distributable ONE-1 checkpoint exists and has an identified owner/version.
- [ ] The model card documents architecture, intended uses, limitations, training/evaluation provenance, license, and hardware requirements.
- [ ] The checkpoint has a reproducible cryptographic digest and a documented integrity-verification process.
- [ ] A compatible local runtime can load the exact checkpoint without hosted inference.
- [ ] Tests cover real prompt generation, context handling, malformed output, unavailable runtime, and model-load errors.
- [ ] Quality, safety, latency, and memory benchmarks have been run and published with methodology.
- [ ] User-facing labels and documentation identify the exact model version and do not overstate capabilities.
- [ ] No weights, credentials, or private training data are committed accidentally.

Until all required items are complete and reviewed, keep the provider status `released: false` and use the explicit planned/not-available wording. This repository patch does not create or train model weights.
