# How O# Releases Are Made

The OmniSharp [release pipeline](https://dev.azure.com/omnisharp/Builds/_build?definitionId=2) runs from the OmniSharp Azure DevOps instance. It is defined in [azure-pipelines.yml](/azure-pipelines.yml).

## Rolling Beta Builds

Merges into the master branch generate an empty draft GitHub release with a beta version tag. The tag created for the release then causes a build that uploads the packages.

```mermaid
sequenceDiagram
  autonumber
  Maintainer ->> GitHub: Merges PR into the `master` branch
  GitHub --) OmniSharp ADO: Merge to `master` triggers pipeline
  activate OmniSharp ADO
  OmniSharp ADO ->> GitHub: Pulls source for omnisharp-roslyn
  note over OmniSharp ADO: Calculates a build version
  OmniSharp ADO ->> GitHub: Creates a draft release and `v#35;.#35;.#35;-beta.#35;` tag
  deactivate OmniSharp ADO
  GitHub --) OmniSharp ADO: `v*` tag creation triggers pipeline
  activate OmniSharp ADO
  OmniSharp ADO ->> GitHub: Pulls source for omnisharp-roslyn
  note over OmniSharp ADO: Builds packages for various platforms
  OmniSharp ADO ->> GitHub: Adds packages to release and unmark as draft
  deactivate OmniSharp ADO
```

## Official Builds

A maintainer creates an empty draft GitHub release with the appropriate version tag. The tag created for the release then causes a build that uploads the packages.

```mermaid
sequenceDiagram
  autonumber
  Maintainer ->> GitHub: Creates draft release with a`v#35;.#35;.#35;` tag
  GitHub --) OmniSharp ADO: `v*` tag creation triggers pipeline
  activate OmniSharp ADO
  OmniSharp ADO ->> GitHub: Pulls source for omnisharp-roslyn
  note over OmniSharp ADO: Builds packages for various platforms
  OmniSharp ADO ->> GitHub: Adds packages to release and unmark as draft
  deactivate OmniSharp ADO
```