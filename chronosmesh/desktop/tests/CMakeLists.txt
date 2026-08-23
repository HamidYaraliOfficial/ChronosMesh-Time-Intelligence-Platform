find_package(Qt6 REQUIRED COMPONENTS Test Widgets Network Sql)

add_executable(tst_thememanager tst_thememanager.cpp ../src/ThemeManager.cpp)
target_link_libraries(tst_thememanager PRIVATE Qt6::Test Qt6::Widgets)
add_test(NAME ThemeManagerTests COMMAND tst_thememanager)

add_executable(tst_translationmanager tst_translationmanager.cpp ../src/TranslationManager.cpp)
target_link_libraries(tst_translationmanager PRIVATE Qt6::Test Qt6::Widgets)
add_test(NAME TranslationManagerTests COMMAND tst_translationmanager)
