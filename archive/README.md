# Archived history

Stow and Furrow were separate mods before they were folded into Vaettir. Their
documentation now lives in this repo's README; what is here is the part that could not
be merged, which is their commit history.

Each `.bundle` is a complete git repository in one file: every commit, branch and tag.
They exist so the GitHub repos can be deleted without losing the reasoning behind
decisions that are still load-bearing in this codebase.

To read either one:

```
git clone stow.bundle stow-history
git -C stow-history log
```

That produces an ordinary repo you can browse and then throw away.

Worth knowing before you go looking: several comments in `src/stow/` refer to arguments
settled in commits that are only in here: why the rules live on the chest's own ZDO
rather than in config, why a carrying spirit is a published trip rather than a networked
prefab, and why the post empties on close instead of continuously.

`stow.bundle` is 39 commits, `furrow.bundle` is 11.
