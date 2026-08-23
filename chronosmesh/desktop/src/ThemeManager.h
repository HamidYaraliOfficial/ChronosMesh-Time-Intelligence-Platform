#pragma once

#include <QObject>
#include <QString>
#include <QMap>
#include <QApplication>

namespace ChronosMesh {

/// Identifies a built-in or dynamically-registered visual theme. New
/// themes can be added at runtime via ThemeManager::registerTheme without
/// touching this enum, using CustomThemeId as the marker; built-in themes
/// keep dedicated enum values purely for fast, readable call sites.
enum class ThemeId {
    Windows11Default,
    Light,
    Dark,
    Blue,
    Red,
    Custom
};

/// Central Theme Engine for the ChronosMesh Desktop Client.
///
/// Themes are plain Qt Style Sheets (.qss) shipped as Qt resources. The
/// engine is intentionally decoupled from the enum above: internally every
/// theme (built-in or custom) is just a named QSS resource path in
/// `m_themes`, so product/design teams can ship additional themes purely as
/// resource + one `registerTheme` call, without recompiling ThemeManager
/// itself.
class ThemeManager : public QObject {
    Q_OBJECT

public:
    static ThemeManager& instance();

    /// Registers a theme (built-in or custom) under `name`, pointing at a
    /// `.qss` resource path (e.g. ":/themes/dark.qss").
    void registerTheme(const QString& name, const QString& qssResourcePath, const QString& displayName);

    /// Applies the theme by internal name to the whole QApplication.
    void applyTheme(const QString& name);
    void applyTheme(ThemeId id);

    QString currentThemeName() const { return m_currentTheme; }
    QStringList availableThemeNames() const { return m_themes.keys(); }
    QString displayNameFor(const QString& name) const { return m_displayNames.value(name, name); }

    static QString idToName(ThemeId id);

signals:
    void themeChanged(const QString& newThemeName);

private:
    explicit ThemeManager(QObject* parent = nullptr);
    void registerBuiltInThemes();

    QMap<QString, QString> m_themes;       // name -> qss resource path
    QMap<QString, QString> m_displayNames; // name -> human-readable label
    QString m_currentTheme;
};

} // namespace ChronosMesh
