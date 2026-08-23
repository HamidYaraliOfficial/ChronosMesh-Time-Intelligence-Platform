#pragma once

#include <QObject>
#include <QTranslator>
#include <QString>
#include <QApplication>

namespace ChronosMesh {

/// Manages runtime language switching and layout direction (LTR/RTL) for
/// the whole application. All user-facing strings must go through Qt's
/// tr()/QCoreApplication::translate() so they resolve through the
/// .ts/.qm files loaded here — no hard-coded UI text anywhere else in the
/// codebase.
class TranslationManager : public QObject {
    Q_OBJECT

public:
    static TranslationManager& instance();

    /// ISO-ish language codes supported out of the box. RTL is derived
    /// automatically from the language, never hard-coded per-widget.
    enum class Language { English, Persian, Chinese };

    void switchLanguage(Language lang);
    void switchLanguage(const QString& code); // "en" | "fa" | "zh"

    Language currentLanguage() const { return m_currentLanguage; }
    QString currentLanguageCode() const;
    bool isRightToLeft() const;

    static QString languageDisplayName(Language lang);
    static QString languageCode(Language lang);

signals:
    void languageChanged(Language newLanguage);

private:
    explicit TranslationManager(QObject* parent = nullptr);

    QTranslator m_translator;
    QTranslator m_qtBaseTranslator; // Qt's own dialog strings (OK/Cancel/etc.)
    Language m_currentLanguage = Language::English;
};

} // namespace ChronosMesh
