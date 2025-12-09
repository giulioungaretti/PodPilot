# GitHub Copilot Custom Instructions



## Tone and Style
Confirm that what I am asking for is correct only if you are sure and have references / data / compipler output / debug output or tests to support that.

# git 
pass `--no-pager` to all git commands that support it to avoid pager issues in PowerShell.
```powershell
git --no-pager status
```


## Commit Workflow

After making code changes, **always**:

1. **Ask for confirmation** before committing:
   - "Would you like me to commit these changes?"

2. **Show a diff** of the changes:
   - Use `git diff` or `git diff --staged` to display what will be committed
   - Summarize the key changes in a readable format

3. **Propose a semantic commit message** following the [Conventional Commits](https://www.conventionalcommits.org/) specification:

   ```
   <type>(<scope>): <description>

   [optional body]

   [optional footer(s)]
   ```

   ### Commit Types
   - `feat`: A new feature
   - `fix`: A bug fix
   - `docs`: Documentation only changes
   - `style`: Changes that do not affect the meaning of the code (white-space, formatting)
   - `refactor`: A code change that neither fixes a bug nor adds a feature
   - `perf`: A code change that improves performance
   - `test`: Adding missing tests or correcting existing tests
   - `build`: Changes that affect the build system or external dependencies
   - `ci`: Changes to CI configuration files and scripts
   - `chore`: Other changes that don't modify src or test files

   ### Examples
   - `feat(auth): add OAuth2 login support`
   - `fix(api): handle null response from external service`
   - `refactor(services): extract caching logic to dedicated service`
   - `perf(discovery): cache paired device lookups for 30 seconds`
   - `docs(readme): update installation instructions`

4. **Wait for user approval** before executing the commit command.

5. **Execute the commit command** with the proposed message if approved. Set both author AND committer using environment variables to avoid dual-author issues:

   ```powershell
   $env:GIT_AUTHOR_NAME = "Giulio Ungaretti"
   $env:GIT_AUTHOR_EMAIL = "giulio.ungaretti@gmail.com"
   $env:GIT_COMMITTER_NAME = "Giulio Ungaretti"
   $env:GIT_COMMITTER_EMAIL = "giulio.ungaretti@gmail.com"
   git commit -m "<message>"
   ```


