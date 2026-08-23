#include "ThemeManager.h"

#include <QFile>
#include <QTextStream>
#include <QDebug>

namespace ChronosMesh {

ThemeManager& ThemeManager::instance() {
    static ThemeManager s_instance;
    return s_instance;
}

ThemeManager::ThemeManager(QObject* parent) : QObject(parent) {
    registerBuiltInThemes();
}

void ThemeManager::registerBuiltInThemes() {
    registerTheme("windows11", ":/themes/windows11.qss", QStringLiteral("Windows 11 Default"));
    registerTheme("light", ":/themes/light.qss", QStringLiteral("Light"));
    registerTheme("dark", ":/themes/dark.qss", QStringLiteral("Dark"));
    registerTheme("blue", ":/themes/blue.qss", QStringLiteral("Blue"));
    registerTheme("red", ":/themes/red.qss", QStringLiteral("Red"));
}

void ThemeManager::registerTheme(const QString& name, const QString& qssResourcePath, const QString& displayName) {
    m_themes.insert(name, qssResourcePath);
    m_displayNames.insert(name, displayName);
}

QString ThemeManager::idToName(ThemeId id) {
    switch (id) {
        case ThemeId::Windows11Default: return QStringLiteral("windows11");
        case ThemeId::Light:            return QStringLiteral("light");
        case ThemeId::Dark:             return QStringLiteral("dark");
        case ThemeId::Blue:             return QStringLiteral("blue");
        case ThemeId::Red:              return QStringLiteral("red");
        default:                        return QStringLiteral("windows11");
    }
}

void ThemeManager::applyTheme(ThemeId id) {
    applyTheme(idToName(id));
}

void ThemeManager::applyTheme(const QString& name) {
    if (!m_themes.contains(name)) {
        qWarning() << "ThemeManager: unknown theme" << name << "- falling back to windows11";
        applyTheme(QStringLiteral("windows11"));
        return;
    }

    QFile file(m_themes.value(name));
    if (!file.open(QIODevice::ReadOnly | QIODevice::Text)) {
        qWarning() << "ThemeManager: could not open stylesheet resource" << m_themes.value(name);
        return;
    }

    QTextStream stream(&file);
    const QString qss = stream.readAll();

    if (auto* app = qobject_cast<QApplication*>(QApplication::instance())) {
        app->setStyleSheet(qss);
    }

    m_currentTheme = name;
    emit themeChanged(name);
}

} // namespace ChronosMesh
