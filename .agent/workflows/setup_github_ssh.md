---
description: How to set up GitHub SSH authentication from scratch
---

# Setting up GitHub SSH Authentication

Since you don't have any SSH keys yet, we need to generate one and add it to your GitHub account.

## Step 1: Generate a new SSH key
Run the following command to generate a new SSH key. We'll use your email for the label.
When prompted for a file, just press Enter to accept the default.
When prompted for a passphrase, you can press Enter for no passphrase (easier) or type one for extra security.

```bash
ssh-keygen -t ed25519 -C "ravaldeep063@gmail.com" -f ~/.ssh/id_ed25519
```

## Step 2: Start the SSH Agent and Add the Key
Now we need to ensure the system authentication agent is running and add your new key to it.

```bash
eval "$(ssh-agent -s)"
ssh-add --apple-use-keychain ~/.ssh/id_ed25519
```

## Step 3: Copy your Public Key
You need to copy the contents of the public key to your clipboard so you can paste it into GitHub.

```bash
pbcopy < ~/.ssh/id_ed25519.pub
echo "Your public SSH key has been copied to your clipboard!"
```

## Step 4: Add the Key to GitHub
1. Open your browser and go to [GitHub SSH Keys Settings](https://github.com/settings/keys).
2. Click the green **"New SSH key"** button.
3. **Title**: Give it a name like "My iMac" or "Work Laptop".
4. **Key Type**: Leave it as "Authentication Key".
5. **Key**: Paste the key from your clipboard (Cmd+V).
6. Click **"Add SSH key"**.

## Step 5: Test the Connection
Verify that everything is working.

```bash
ssh -T git@github.com
```
*Type 'yes' if asked about authenticity.*

## Step 6: Push your code
Now you can finally push your changes!

```bash
git push -u origin main
```
