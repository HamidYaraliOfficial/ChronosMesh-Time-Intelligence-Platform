#include "TranslationManager.h"

#include <QLibraryInfo>
#include <QLocale>
#include <QDebug>

namespace ChronosMesh {

TranslationManager& TranslationManager::instance() {
    static TranslationManager s_instance;
    return s_instance;
}

TranslationManager::TranslationManager(QObject* parent) : QObject(parent) {}

QString TranslationManager::languageCode(Language lang) {
    switch (lang) {
        case Language::English: return QStringLiteral("en");
        case Language::Persian: return QStringLiteral("fa");
        case Language::Chinese: return QStringLiteral("zh");
    }
    return QStringLiteral("en");
}

QString TranslationManager::languageDisplayName(Language lang) {
    switch (lang) {
        case Language::English: return QStringLiteral("English");
        case Language::Persian: return QStringLiteral("فارسی");
        case Language::Chinese: return QStringLiteral("中文");
    }
    return QStringLiteral("English");
}

QString TranslationManager::currentLanguageCode() const {
    return languageCode(m_currentLanguage);
}

bool TranslationManager::isRightToLeft() const {
    // Persian is the only RTL language ChronosMesh ships today; adding
    // Arabic/Hebrew later only requires extending this switch, not
    // touching any view code (views read layoutDirection() from the
    // application, never hard-code direction).
    return m_currentLanguage == Language::Persian;
}

void TranslationManager::switchLanguage(const QString& code) {
    if (code == QStringLiteral("fa")) switchLanguage(Language::Persian);
    else if (code == QStringLiteral("zh")) switchLanguage(Language::Chinese);
    else switchLanguage(Language::English);
}

void TranslationManager::switchLanguage(Language lang) {
    auto* app = QApplication::instance();
    if (!app) return;

    app->removeTranslator(&m_translator);
    app->removeTranslator(&m_qtBaseTranslator);

    const QString code = languageCode(lang);

    // Load ChronosMesh's own translation catalogue.
    if (m_translator.load(QStringLiteral(":/translations/chronosmesh_%1.qm").arg(code))) {
        app->installTranslator(&m_translator);
    } else {
        qWarning() << "TranslationManager: could not load catalogue for" << code;
    }

    // Load Qt's own built-in strings (standard dialog buttons, etc.) for
    // the same locale, when shipped alongside the Qt installation.
    const QString qtTranslationsPath = QLibraryInfo::path(QLibraryInfo::TranslationsPath);
    if (m_qtBaseTranslator.load(QStringLiteral("qtbase_%1").arg(code), qtTranslationsPath)) {
        app->installTranslator(&m_qtBaseTranslator);
    }

    m_currentLanguage = lang;

    // Layout direction cascades to every widget (menus, toolbars, dialogs,
    // calendar grid, tables, forms) automatically once set at the
    // application level.
    app->setLayoutDirection(isRightToLeft() ? Qt::RightToLeft : Qt::LeftToRight);

    QLocale::setDefault(QLocale(code));

    emit languageChanged(lang);
}

} // namespace ChronosMesh
