## Your checkout

`/home/agent/code/altinn-notifications` is mounted from the machine of the person who created you, rather than cloned.
Work in it on the branch it has. What you change there changes on their machine at once, so leave alone the changes
and branches that are not yours.

When Git says it is not a Git repository, the person applied you from a linked worktree, whose metadata is outside the
mount. Tell them to apply you from a clone instead, rather than working around it.
