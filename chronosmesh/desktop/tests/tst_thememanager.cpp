// Unit tests for the Theme Engine and Translation Manager, run via CTest +
// QTest. Build with -DBUILD_TESTING=ON (see CMakeLists.txt in this
// directory) and run `ctest` from the build folder.

#include <QtTest/QtTest>
#include "../src/ThemeManager.h"
#include "../src/TranslationManager.h"

using namespace ChronosMesh;

class TestThemeManager : public QObject {
    Q_OBJECT
private slots:
    void hasFiveBuiltInThemes() {
        const auto names = ThemeManager::instance().availableThemeNames();
        QVERIFY(names.contains("windows11"));
        QVERIFY(names.contains("light"));
        QVERIFY(names.contains("dark"));
        QVERIFY(names.contains("blue"));
        QVERIFY(names.contains("red"));
        QCOMPARE(names.size(), 5);
    }

    void customThemeCanBeRegisteredAtRuntime() {
        ThemeManager::instance().registerTheme("midnight", ":/themes/dark.qss", "Midnight");
        QVERIFY(ThemeManager::instance().availableThemeNames().contains("midnight"));
        QCOMPARE(ThemeManager::instance().displayNameFor("midnight"), QString("Midnight"));
    }

    void idToNameMapsCorrectly() {
        QCOMPARE(ThemeManager::idToName(ThemeId::Dark), QString("dark"));
        QCOMPARE(ThemeManager::idToName(ThemeId::Blue), QString("blue"));
        QCOMPARE(ThemeManager::idToName(ThemeId::Red), QString("red"));
    }
};

QTEST_MAIN(TestThemeManager)
#include "tst_thememanager.moc"
