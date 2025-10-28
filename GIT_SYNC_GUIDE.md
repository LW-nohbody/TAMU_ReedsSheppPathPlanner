# How to Sync Your Branch with Main Before Pull Request

## Current Situation
- You're on: `Ali_Branch`
- Main branch on GitHub may have new commits
- You want to: Get those changes + keep your changes + make a PR

## Step-by-Step Process

### 1. Commit Your Current Changes First
```bash
# Add all your changes
git add .

# Commit with a descriptive message
git commit -m "Complete rewrite: Simple dig system with Reeds-Shepp paths

- Created SimpleDigLogic.cs for smart peak-flattening algorithm
- Rewrote VehicleBrain.cs with clean state machine
- Made dig radius relative to robot width
- Robots always dig highest point to avoid pit formation
- All movement uses pure Reeds-Shepp paths"
```

### 2. Fetch Latest Changes from GitHub
```bash
# Get all the latest updates from the remote repository
git fetch origin
```

### 3. Rebase Your Branch on Top of Main (RECOMMENDED)
This creates a clean, linear history:
```bash
# Rebase your Ali_Branch on top of the latest main
git rebase origin/main
```

**If there are conflicts:**
- Git will pause and tell you which files have conflicts
- Open those files and look for `<<<<<<< HEAD` markers
- Edit to resolve conflicts (keep both changes, or choose one)
- After fixing each file:
  ```bash
  git add <fixed-file>
  git rebase --continue
  ```
- Repeat until rebase completes

**To abort if it gets messy:**
```bash
git rebase --abort  # Goes back to before rebase
```

### 4. Alternative: Merge Main into Your Branch
If you prefer merge instead of rebase:
```bash
# Merge main branch into your current branch
git merge origin/main
```

This creates a merge commit but preserves all history.

### 5. Push Your Updated Branch
```bash
# Force push if you used rebase (rewrites history)
git push origin Ali_Branch --force-with-lease

# Regular push if you used merge
git push origin Ali_Branch
```

### 6. Create Pull Request on GitHub
1. Go to: https://github.com/YOUR_USERNAME/TAMU_ReedsSheppPathPlanner
2. Click "Pull requests" → "New pull request"
3. Set:
   - Base: `main`
   - Compare: `Ali_Branch`
4. Add title: "Simple dig system with Reeds-Shepp path planning"
5. Add description of changes
6. Click "Create pull request"

## Quick Command Sequence (Rebase Method)
```bash
# 1. Commit your work
git add .
git commit -m "Simple dig system rewrite"

# 2. Get latest from main and rebase
git fetch origin
git rebase origin/main

# 3. Resolve any conflicts, then:
git push origin Ali_Branch --force-with-lease
```

## Quick Command Sequence (Merge Method - Safer)
```bash
# 1. Commit your work
git add .
git commit -m "Simple dig system rewrite"

# 2. Get latest from main and merge
git fetch origin
git merge origin/main

# 3. Resolve any conflicts, then:
git push origin Ali_Branch
```

## Which Method to Use?

### Use REBASE if:
- ✅ You want clean, linear history
- ✅ You haven't pushed these commits yet
- ✅ You're comfortable resolving conflicts

### Use MERGE if:
- ✅ You want to preserve exact history
- ✅ You've already pushed and others might have pulled
- ✅ You want simpler conflict resolution

## Checking What's Different
Before syncing, see what changed on main:
```bash
# See commits on main that you don't have
git log origin/main ^Ali_Branch

# See files that changed on main
git diff Ali_Branch..origin/main --name-only
```

## Pro Tips
- **Always commit before syncing** - Never sync with uncommitted changes
- **Use `--force-with-lease` not `--force`** - Safer for force-pushing
- **Test after syncing** - Make sure everything still works
- **Write good commit messages** - Helps reviewers understand changes

## If Something Goes Wrong
```bash
# See what you did recently
git reflog

# Go back to before the mess
git reset --hard HEAD@{n}  # where n is the step you want

# Or just start over from remote
git fetch origin
git reset --hard origin/Ali_Branch
```
