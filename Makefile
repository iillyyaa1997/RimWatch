# RimWatch Makefile
# Convenient commands for Docker-based development

.PHONY: help build test test-unit test-integration test-quick t dev quick-build release clean logs shell format lint setup install package deploy docker-info status

# Default target
.DEFAULT_GOAL := help

# Colors for output
CYAN=\033[0;36m
GREEN=\033[0;32m
YELLOW=\033[1;33m
RED=\033[0;31m
NC=\033[0m # No Color

## 🆘 Show this help message
help:
	@echo ""
	@echo "$(CYAN)RimWatch Development Commands$(NC)"
	@echo ""
	@echo "$(GREEN)⚡ Quick Start:$(NC)"
	@echo "  $(YELLOW)make deploy$(NC)       - Build + Install in one command (recommended)"
	@echo ""
	@echo "$(GREEN)🏗️  Build Commands:$(NC)"
	@echo "  $(YELLOW)make build$(NC)        - Full build in Docker container"
	@echo "  $(YELLOW)make quick-build$(NC)  - Quick incremental build"
	@echo "  $(YELLOW)make release$(NC)      - Production release build"
	@echo ""
	@echo "$(GREEN)🧪 Test Commands:$(NC)"
	@echo "  $(YELLOW)make test$(NC)         - Run all tests in Docker"
	@echo "  $(YELLOW)make test-unit$(NC)    - Run unit tests only"
	@echo "  $(YELLOW)make test-integration$(NC) - Run integration tests only"
	@echo "  $(YELLOW)make test-quick$(NC)   - Quick test menu"
	@echo "  $(YELLOW)make t$(NC)            - Super quick test (unit tests only)"
	@echo ""
	@echo "$(GREEN)🚀 Development Commands:$(NC)"
	@echo "  $(YELLOW)make dev$(NC)          - Start development environment"
	@echo "  $(YELLOW)make shell$(NC)        - Enter Docker container shell"
	@echo "  $(YELLOW)make logs$(NC)         - Show Docker container logs"
	@echo "  $(YELLOW)make install$(NC)      - Install/Update mod in RimWorld"
	@echo ""
	@echo "$(GREEN)🧹 Maintenance Commands:$(NC)"
	@echo "  $(YELLOW)make clean$(NC)        - Clean Docker images and containers"
	@echo "  $(YELLOW)make clean-all$(NC)    - Deep clean (images, volumes, cache)"
	@echo ""
	@echo "$(GREEN)🎨 Code Quality Commands:$(NC)"
	@echo "  $(YELLOW)make format$(NC)       - Format code (dotnet format)"
	@echo "  $(YELLOW)make format-fix$(NC)   - Auto-fix code style issues"
	@echo "  $(YELLOW)make format-check$(NC) - Check code style without fixing"
	@echo "  $(YELLOW)make lint$(NC)         - Run code analysis"
	@echo ""
	@echo "$(GREEN)📋 Setup Commands:$(NC)"
	@echo "  $(YELLOW)make setup$(NC)        - Initial project setup"
	@echo "  $(YELLOW)make docker-info$(NC)  - Show Docker environment info"
	@echo ""

## 🏗️ Build the project in Docker
build:
	@echo "$(CYAN)🏗️ Building RimWatch in Docker...$(NC)"
	@echo "$(YELLOW)📝 Using volume mounts for latest code...$(NC)"
	docker-compose up build --remove-orphans
	@echo "$(GREEN)✅ Build completed!$(NC)"

## ⚡ Quick incremental build
quick-build:
	@echo "$(CYAN)⚡ Quick build...$(NC)"
	docker-compose up quick-build --remove-orphans
	@echo "$(GREEN)✅ Quick build completed!$(NC)"

## 🚀 Production release build
release:
	@echo "$(CYAN)🚀 Creating release build...$(NC)"
	docker-compose up release --remove-orphans
	@echo "$(GREEN)✅ Release build completed!$(NC)"

## 🧪 Run all tests
test:
	@echo "$(CYAN)🧪 Running all tests...$(NC)"
	docker-compose up test --remove-orphans
	@echo "$(GREEN)✅ Tests completed!$(NC)"

## 🔬 Run unit tests only
test-unit:
	@echo "$(CYAN)🔬 Running unit tests...$(NC)"
	docker-compose run test bash -c "cd /app/Tests && dotnet test --filter Category=Unit --logger \"console;verbosity=normal\""
	@echo "$(GREEN)✅ Unit tests completed!$(NC)"

## 🔗 Run integration tests only
test-integration:
	@echo "$(CYAN)🔗 Running integration tests...$(NC)"
	docker-compose run test bash -c "cd /app/Tests && dotnet test --filter Category=Integration --logger \"console;verbosity=normal\""
	@echo "$(GREEN)✅ Integration tests completed!$(NC)"

## 🚀 Quick test shortcuts
test-quick:
	@echo "$(CYAN)⚡ Quick Test Menu$(NC)"
	@echo "$(YELLOW)What do you want to test?$(NC)"
	@echo "  1) Unit tests (fast)"
	@echo "  2) Integration tests"
	@echo "  3) Everything"
	@echo ""
	@read -p "Choice (1-3): " choice; \
	case "$$choice" in \
		1) make test-unit;; \
		2) make test-integration;; \
		3) make test;; \
		*) echo "$(RED)❌ Invalid choice$(NC)";; \
	esac

## ⚡ Super quick test - fastest option for development
t:
	@echo "$(CYAN)⚡ Running unit tests (fastest)...$(NC)"
	@docker-compose run test bash -c "cd /app/Tests && dotnet test --filter \"Category=Unit\" --logger \"console;verbosity=minimal\""
	@echo "$(GREEN)✅ Quick tests completed!$(NC)"

## 💻 Start development environment
dev:
	@echo "$(CYAN)💻 Starting development environment...$(NC)"
	docker-compose up dev

## 🐚 Enter Docker container shell for debugging
shell:
	@echo "$(CYAN)🐚 Entering Docker container shell...$(NC)"
	docker-compose run --rm build /bin/bash

## 📋 Show Docker container logs
logs:
	@echo "$(CYAN)📋 Showing Docker logs...$(NC)"
	docker-compose logs -f

## 🧹 Clean Docker images and containers
clean:
	@echo "$(CYAN)🧹 Cleaning Docker environment...$(NC)"
	docker-compose down --volumes --remove-orphans
	docker system prune -f
	@echo "$(GREEN)✅ Cleanup completed!$(NC)"

## 💥 Deep clean - remove everything
clean-all:
	@echo "$(RED)💥 Deep cleaning Docker environment...$(NC)"
	@echo "$(YELLOW)⚠️  This will remove ALL Docker containers, images, and volumes!$(NC)"
	@read -p "Are you sure? (y/N): " confirm && [ "$$confirm" = "y" ] || exit 1
	docker-compose down --volumes --remove-orphans
	docker system prune -af --volumes
	docker volume prune -f
	@echo "$(GREEN)✅ Deep cleanup completed!$(NC)"

## ✨ Format code using dotnet format
format:
	@echo "$(CYAN)✨ Formatting code...$(NC)"
	docker-compose run --rm build bash -c "cd /app/Source/RimWatch && dotnet format --verbosity normal"
	@echo "$(GREEN)✅ Code formatting completed!$(NC)"

## 🔍 Run code analysis and linting
lint:
	@echo "$(CYAN)🔍 Running code analysis...$(NC)"
	docker-compose run --rm build bash -c "cd /app/Source/RimWatch && dotnet build --verbosity normal --configuration Debug"
	@echo "$(GREEN)✅ Code analysis completed!$(NC)"

## 🧹 Format code and fix style issues automatically
format-fix:
	@echo "$(CYAN)🧹 Auto-fixing code style issues...$(NC)"
	docker-compose run --rm build bash -c "cd /app/Source/RimWatch && dotnet format whitespace --verbosity normal"
	@echo "$(GREEN)✅ Auto-fix completed!$(NC)"

## 🔎 Check code style without fixing (whitespace only)
format-check:
	@echo "$(CYAN)🔎 Checking code style...$(NC)"
	docker-compose run --rm build bash -c "cd /app/Source/RimWatch && dotnet format whitespace --verify-no-changes --verbosity normal"
	@echo "$(GREEN)✅ Code style check completed!$(NC)"

## 📦 Initial project setup
setup: docker-info
	@echo "$(CYAN)📦 Setting up RimWatch development environment...$(NC)"
	@echo "$(YELLOW)📋 Checking prerequisites...$(NC)"
	@command -v docker >/dev/null 2>&1 || { echo "$(RED)❌ Docker is required but not installed.$(NC)" >&2; exit 1; }
	@command -v docker-compose >/dev/null 2>&1 || { echo "$(RED)❌ Docker Compose is required but not installed.$(NC)" >&2; exit 1; }
	@echo "$(GREEN)✅ Docker and Docker Compose are available$(NC)"
	@echo "$(YELLOW)🏗️ Building initial Docker images...$(NC)"
	docker-compose build
	@echo "$(GREEN)✅ Setup completed! Use 'make help' to see available commands.$(NC)"

## 🐳 Show Docker environment info
docker-info:
	@echo "$(CYAN)🐳 Docker Environment Information:$(NC)"
	@echo "$(YELLOW)Docker version:$(NC)"
	@docker --version 2>/dev/null || echo "$(RED)❌ Docker not found$(NC)"
	@echo "$(YELLOW)Docker Compose version:$(NC)"
	@docker-compose --version 2>/dev/null || echo "$(RED)❌ Docker Compose not found$(NC)"
	@echo "$(YELLOW)Docker status:$(NC)"
	@docker info --format "{{.ServerVersion}}" 2>/dev/null && echo "$(GREEN)✅ Docker daemon running$(NC)" || echo "$(RED)❌ Docker daemon not running$(NC)"

## 📦 Create distribution package (RimWorld 1.6 compatible)
package: build
	@echo "$(CYAN)📦 Creating distribution package...$(NC)"
	@mkdir -p dist/RimWatch/About dist/RimWatch/Assemblies
	@cp -r About/* dist/RimWatch/About/ 2>/dev/null || echo "$(YELLOW)⚠️  About folder not found$(NC)"
	@if [ -f 1.6/Assemblies/RimWatch.dll ]; then \
		cp 1.6/Assemblies/RimWatch.dll dist/RimWatch/Assemblies/; \
		echo "$(GREEN)✅ Copied RimWorld 1.6 compatible DLL$(NC)"; \
	else \
		echo "$(RED)❌ 1.6/Assemblies/RimWatch.dll not found, run 'make build' first$(NC)"; \
		exit 1; \
	fi
	@cp README.md LICENSE dist/RimWatch/ 2>/dev/null || echo "$(YELLOW)⚠️  README.md or LICENSE not found$(NC)"
	@cp -r Languages dist/RimWatch/ 2>/dev/null || echo "$(YELLOW)⚠️  Languages folder not found$(NC)"
	@echo "$(GREEN)✅ Package created in dist/RimWatch/ (RimWorld 1.5/1.6 compatible structure)$(NC)"

## 🚀 Install/Update mod in RimWorld mods directory
install: package
	@echo "$(CYAN)🚀 Installing/Updating mod in RimWorld...$(NC)"
	@if [ -f .env ]; then \
		RIMWORLD_MODS=$$(grep '^RIMWORLD_MODS_PATH=' .env | cut -d '=' -f2- | sed 's/^"//;s/"$$//'); \
		RIMWORLD_MODS=$${RIMWORLD_MODS:-$(HOME)/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods}; \
	else \
		echo "$(YELLOW)⚠️  .env file not found, using default path$(NC)"; \
		RIMWORLD_MODS="$(HOME)/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods"; \
	fi; \
	RIMWORLD_MODS=$$(eval echo "$$RIMWORLD_MODS"); \
	if [ ! -d "$$RIMWORLD_MODS" ]; then \
		echo "$(RED)❌ RimWorld mods directory not found: $$RIMWORLD_MODS$(NC)"; \
		echo "$(YELLOW)💡 Please create .env file with RIMWORLD_MODS_PATH variable$(NC)"; \
		echo "$(YELLOW)💡 See .env.example for reference$(NC)"; \
		exit 1; \
	fi; \
	if [ -d "$$RIMWORLD_MODS/RimWatch" ]; then \
		echo "$(YELLOW)📦 Mod found, updating...$(NC)"; \
		rm -rf "$$RIMWORLD_MODS/RimWatch"; \
	else \
		echo "$(YELLOW)📦 Mod not found, installing...$(NC)"; \
	fi; \
	cp -r dist/RimWatch/ "$$RIMWORLD_MODS/RimWatch/"; \
	chmod 644 "$$RIMWORLD_MODS/RimWatch/Assemblies"/*.dll 2>/dev/null || true; \
	rm -f "$$RIMWORLD_MODS/RimWatch/About/Preview.png" 2>/dev/null || true; \
	echo "$(GREEN)✅ Mod installed/updated successfully!$(NC)"; \
	echo "$(CYAN)📍 Location: $$RIMWORLD_MODS/RimWatch/$(NC)"

## ⚡ Quick deploy: Build + Install in one command
deploy: build
	@echo "$(CYAN)⚡ Quick Deploy: Building and installing...$(NC)"
	@$(MAKE) --no-print-directory package-internal
	@$(MAKE) --no-print-directory install-internal
	@echo "$(GREEN)🎉 Deploy complete! Mod is ready to use in RimWorld.$(NC)"

# Internal targets (no logging spam)
package-internal:
	@mkdir -p dist/RimWatch/About dist/RimWatch/Assemblies
	@cp -r About/* dist/RimWatch/About/ 2>/dev/null || true
	@if [ -f 1.6/Assemblies/RimWatch.dll ]; then \
		cp 1.6/Assemblies/RimWatch.dll dist/RimWatch/Assemblies/; \
	else \
		echo "$(RED)❌ Build failed$(NC)"; exit 1; \
	fi
	@cp README.md LICENSE dist/RimWatch/ 2>/dev/null || true
	@cp -r Languages dist/RimWatch/ 2>/dev/null || true

install-internal:
	@if [ -f .env ]; then \
		RIMWORLD_MODS=$$(grep '^RIMWORLD_MODS_PATH=' .env | cut -d '=' -f2- | sed 's/^"//;s/"$$//'); \
		RIMWORLD_MODS=$${RIMWORLD_MODS:-$(HOME)/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods}; \
	else \
		RIMWORLD_MODS="$(HOME)/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods"; \
	fi; \
	RIMWORLD_MODS=$$(eval echo "$$RIMWORLD_MODS"); \
	if [ ! -d "$$RIMWORLD_MODS" ]; then \
		echo "$(RED)❌ RimWorld mods directory not found$(NC)"; exit 1; \
	fi; \
	rm -rf "$$RIMWORLD_MODS/RimWatch" 2>/dev/null || true; \
	cp -r dist/RimWatch/ "$$RIMWORLD_MODS/RimWatch/"; \
	chmod 644 "$$RIMWORLD_MODS/RimWatch/Assemblies"/*.dll 2>/dev/null || true; \
	rm -f "$$RIMWORLD_MODS/RimWatch/About/Preview.png" 2>/dev/null || true; \
	echo "$(GREEN)✅ Installed to: $$RIMWORLD_MODS/RimWatch/$(NC)"

# Development workflow shortcuts

## 🔄 Full development cycle: clean -> build -> test
cycle: clean build test
	@echo "$(GREEN)🎉 Full development cycle completed!$(NC)"

## ⚡ Quick development cycle: build -> test
quick-cycle: quick-build test
	@echo "$(GREEN)🎉 Quick development cycle completed!$(NC)"

## 🌿 Git status with Docker build info
status:
	@echo "$(CYAN)🌿 Git and Docker Status:$(NC)"
	@echo "$(YELLOW)📋 Git status:$(NC)"
	@git status --short 2>/dev/null || echo "$(RED)❌ Not a git repository$(NC)"
	@echo "$(YELLOW)🏗️ Last build status:$(NC)"
	@[ -f Build/Assemblies/RimWatch.dll ] && echo "$(GREEN)✅ Build exists$(NC)" || echo "$(RED)❌ No build found$(NC)"
	@echo "$(YELLOW)🐳 Docker containers:$(NC)"
	@docker-compose ps

