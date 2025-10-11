# Container Runtime Setup Guide

This guide helps you install and configure either Docker or Podman for MediaSet development.

## Option 1: Docker (Recommended for most users)

### Windows
1. Download Docker Desktop from [docker.com](https://www.docker.com/products/docker-desktop/)
2. Install and restart your computer
3. Start Docker Desktop
4. Verify installation: `docker --version && docker-compose --version`

### macOS
1. Download Docker Desktop from [docker.com](https://www.docker.com/products/docker-desktop/)
2. Install the `.dmg` file
3. Start Docker Desktop from Applications
4. Verify installation: `docker --version && docker-compose --version`

### Linux (Ubuntu/Debian)
```bash
# Install Docker
sudo apt update
sudo apt install docker.io docker-compose

# Add your user to docker group (logout/login required)
sudo usermod -aG docker $USER

# Start Docker service
sudo systemctl start docker
sudo systemctl enable docker

# Verify installation
docker --version && docker-compose --version
```

### Linux (RHEL/CentOS/Fedora)
```bash
# Install Docker
sudo dnf install docker docker-compose

# Add your user to docker group (logout/login required)
sudo usermod -aG docker $USER

# Start Docker service
sudo systemctl start docker
sudo systemctl enable docker

# Verify installation
docker --version && docker-compose --version
```

## Option 2: Podman (Recommended for Linux rootless containers)

### Linux (Ubuntu/Debian)
```bash
# Install Podman
sudo apt update
sudo apt install podman

# Install podman-compose
pip3 install podman-compose
# OR install docker-compose to use with Podman
sudo apt install docker-compose

# Start Podman socket (for rootless usage)
systemctl --user start podman.socket
systemctl --user enable podman.socket

# Verify installation
podman --version
```

### Linux (RHEL/CentOS/Fedora)
```bash
# Install Podman (usually pre-installed on newer versions)
sudo dnf install podman

# Install podman-compose
pip3 install podman-compose
# OR install docker-compose to use with Podman
sudo dnf install docker-compose

# Start Podman socket (for rootless usage)
systemctl --user start podman.socket
systemctl --user enable podman.socket

# Verify installation
podman --version
```

### macOS
```bash
# Install via Homebrew
brew install podman

# Install podman-compose
pip3 install podman-compose

# Initialize Podman machine (required on macOS)
podman machine init
podman machine start

# Verify installation
podman --version
```

### Windows
```bash
# Install via Chocolatey
choco install podman-desktop

# Or download from: https://podman.io/getting-started/installation#windows
# Follow the installer instructions

# Install podman-compose via pip
pip install podman-compose

# Verify installation
podman --version
```

## Verification

After installation, verify your setup:

```bash
# Clone MediaSet (if you haven't already)
git clone https://github.com/paulmfischer/MediaSet.git
cd MediaSet

# Test the development script
./dev.sh start
```

You should see output like:
- `✅ Found Docker` or `✅ Found Podman`
- `✅ Using docker-compose` or `✅ Using podman-compose`

## Troubleshooting

### Docker Issues

**Permission Denied (Linux):**
```bash
# Make sure you're in the docker group
groups $USER

# If 'docker' is not listed, add yourself and logout/login
sudo usermod -aG docker $USER
```

**Docker Desktop not starting (Windows/macOS):**
- Restart Docker Desktop
- Check if virtualization is enabled in BIOS
- Try running as administrator

### Podman Issues

**Socket not starting:**
```bash
# Check socket status
systemctl --user status podman.socket

# Restart if needed
systemctl --user restart podman.socket
```

**SELinux issues (RHEL/Fedora/CentOS):**
```bash
sudo setsebool -P container_manage_cgroup true
```

**Rootless issues:**
```bash
# Check if user namespaces are configured
podman unshare cat /proc/self/uid_map

# If empty, configure subuid/subgid
echo "$USER:100000:65536" | sudo tee -a /etc/subuid
echo "$USER:100000:65536" | sudo tee -a /etc/subgid
```

## Next Steps

Once your container runtime is set up:

1. Run `./dev.sh start` to start the development environment
2. Access the application at http://localhost:3000
3. See [DEVELOPMENT.md](DEVELOPMENT.md) for detailed development workflow