#include <QtTest/QtTest>
#include "../src/TranslationManager.h"

using namespace ChronosMesh;

class TestTranslationManager : public QObject {
    Q_OBJECT
private slots:
    void persianIsRightToLeft() {
        TranslationManager::instance().switchLanguage(TranslationManager::Language::Persian);
        QVERIFY(TranslationManager::instance().isRightToLeft());
        QCOMPARE(TranslationManager::instance().currentLanguageCode(), QString("fa"));
    }

    void englishAndChineseAreLeftToRight() {
        TranslationManager::instance().switchLanguage(TranslationManager::Language::English);
        QVERIFY(!TranslationManager::instance().isRightToLeft());

        TranslationManager::instance().switchLanguage(TranslationManager::Language::Chinese);
        QVERIFY(!TranslationManager::instance().isRightToLeft());
        QCOMPARE(TranslationManager::instance().currentLanguageCode(), QString("zh"));
    }

    void languageCodeRoundTrips() {
        QCOMPARE(TranslationManager::languageCode(TranslationManager::Language::English), QString("en"));
        QCOMPARE(TranslationManager::languageCode(TranslationManager::Language::Persian), QString("fa"));
        QCOMPARE(TranslationManager::languageCode(TranslationManager::Language::Chinese), QString("zh"));
    }
};

QTEST_MAIN(TestTranslationManager)
#include "tst_translationmanager.moc"
