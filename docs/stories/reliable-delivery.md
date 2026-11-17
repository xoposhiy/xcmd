---
id: ReliableDelivery
title: "Deliver and maintain a reliable application"
order: 11
role: developer
---

# Deliver and maintain a reliable application

As a developer, I want to package, start, diagnose, and maintain Xcmd consistently so
that new workflows can reach the running application safely.

[All epics](README.md)

## PackageInputs

As a developer, I want to build the application from declared, versioned inputs so that
I can reproduce builds and manage dependencies explicitly.

## ApplicationLifecycle

As a developer, I want to compose the desktop host and dispose its background work
predictably so that I can diagnose failures and add services with clear lifetimes.

## WorkspaceStorage

As a developer, I want to persist workspace state independently of the UI so that I can
change storage formats without coupling them to views.

## FeedbackIdentity

As a developer, I want to resolve an authored control to a stable source identifier so
that I can locate reported interface targets independently of visual position.

## FeedbackQueue

As a developer, I want to list, claim, close, release, and reopen feedback items
transactionally so that I can implement requested refinements without conflicting agent
batches.

## HeadlessVerification

As a developer, I want to drive application workflows and inspect failures without
visible windows so that I can verify changes without human assistance.

## IntegrationVerification

As a developer, I want to exercise real integration boundaries with isolated fixtures so
that I can detect platform failures without changing the user's files or desktop.

## QualityChecks

As a developer, I want automated checks of code boundaries and quality limits so that I
can catch violations before delivering a change.

## BrowseStories

As a developer, I want to browse epics and their user stories in reading order so that
I can learn the product and find the need behind a change.

## TraceStoryFiles

As a developer, I want to see the files that implement or test a user story so that
I can locate related code and review how responsibilities are spread across files.

## CodeHealth

As a developer, I want to inspect code measurements and their verification evidence so
that I can choose where to investigate or refactor.

## ReleaseHandoff

As a developer, I want to hand off work safely to a new local release so that I can
deliver improvements without interrupting the user's session.

## WindowsIdentity

As a developer, I want to keep the Windows application identity stable so that I can
replace builds without breaking shortcuts and taskbar integration.
