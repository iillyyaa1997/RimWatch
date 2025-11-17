#!/bin/bash

# RimWatch Build Script
# Простой скрипт для сборки проекта

set -e

echo "🎭 RimWatch Builder"
echo "=================="
echo ""

# Проверка Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker не установлен!"
    echo "Установите Docker Desktop: https://www.docker.com/products/docker-desktop"
    exit 1
fi

# Проверка Docker daemon
if ! docker info &> /dev/null; then
    echo "❌ Docker daemon не запущен!"
    echo "Запустите Docker Desktop:"
    echo "  macOS: open -a Docker"
    echo "  Windows: Запустите Docker Desktop из меню Пуск"
    exit 1
fi

echo "✅ Docker готов"
echo ""

# Выбор действия
echo "Что сделать?"
echo "1) Собрать проект"
echo "2) Собрать и установить в RimWorld"
echo "3) Быстрая сборка (Debug)"
echo "4) Очистить и пересобрать"
echo ""
read -p "Выбор (1-4): " choice

case $choice in
    1)
        echo "🏗️ Сборка проекта..."
        make build
        ;;
    2)
        echo "🚀 Сборка и установка..."
        make deploy
        ;;
    3)
        echo "⚡ Быстрая сборка..."
        make quick-build
        ;;
    4)
        echo "🧹 Очистка и сборка..."
        make clean
        make build
        ;;
    *)
        echo "❌ Неверный выбор"
        exit 1
        ;;
esac

echo ""
echo "✅ Готово!"

