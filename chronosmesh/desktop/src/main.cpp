// ChronosMesh Desktop Client — Qt6/C++ application entry point.
// Responsible for bootstrapping the Theme Engine, Translation Manager, and
// the main application shell. See README.markdown "Architecture" for how
// this component fits into the wider ChronosMesh platform.

#include <QApplication>
#include <QSettings>
#include <QLocale>

#include "MainWindow.h"
#include "ThemeManager.h"
#include "TranslationManager.h"

int main(int argc, char* argv[]) {
    QApplication app(argc, argv);
    QApplication::setApplicationName(QStringLiteral("ChronosMesh"));
    QApplication::setOrganizationName(QStringLiteral("ChronosMesh"));
    QApplication::setApplicationVersion(QStringLiteral("1.0.0"));

    // Restore persisted user preferences (theme, language) or fall back to
    // sensible defaults (Windows 11 theme, system locale mapped to one of
    // the three supported languages).
    QSettings settings;
    const QString savedTheme = settings.value(QStringLiteral("appearance/theme"), QStringLiteral("windows11")).toString();
    QString savedLanguage = settings.value(QStringLiteral("appearance/language")).toString();
    if (savedLanguage.isEmpty()) {
        const QString systemLocale = QLocale::system().name(); // e.g. "fa_IR", "zh_CN"
        if (systemLocale.startsWith(QStringLiteral("fa"))) savedLanguage = QStringLiteral("fa");
        else if (systemLocale.startsWith(QStringLiteral("zh"))) savedLanguage = QStringLiteral("zh");
        else savedLanguage = QStringLiteral("en");
    }

    ChronosMesh::TranslationManager::instance().switchLanguage(savedLanguage);
    ChronosMesh::ThemeManager::instance().applyTheme(savedTheme);

    ChronosMesh::MainWindow window;
    window.show();

    const int result = app.exec();

    settings.setValue(QStringLiteral("appearance/theme"), ChronosMesh::ThemeManager::instance().currentThemeName());
    settings.setValue(QStringLiteral("appearance/language"), ChronosMesh::TranslationManager::instance().currentLanguageCode());

    return result;
}
