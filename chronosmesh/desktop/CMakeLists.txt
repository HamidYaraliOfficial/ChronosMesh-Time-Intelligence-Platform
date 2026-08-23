cmake_minimum_required(VERSION 3.21)
project(ChronosMeshDesktop VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# ChronosMesh Desktop targets Windows 11 primarily but is built with pure
# Qt6/CMake so it also compiles on Linux/macOS for development.
find_package(Qt6 REQUIRED COMPONENTS Widgets Network Sql LinguistTools)

qt_standard_project_setup()

set(SOURCES
    src/main.cpp
    src/MainWindow.cpp
    src/ThemeManager.cpp
    src/TranslationManager.cpp
    src/CalendarView.cpp
    src/AvailabilityWidget.cpp
    src/CommandPalette.cpp
    src/ApiClient.cpp
    src/OfflineCache.cpp
)

set(HEADERS
    src/MainWindow.h
    src/ThemeManager.h
    src/TranslationManager.h
    src/CalendarView.h
    src/AvailabilityWidget.h
    src/CommandPalette.h
    src/ApiClient.h
    src/OfflineCache.h
)

qt_add_executable(ChronosMeshDesktop WIN32
    ${SOURCES}
    ${HEADERS}
    resources/resources.qrc
)

target_link_libraries(ChronosMeshDesktop PRIVATE
    Qt6::Widgets
    Qt6::Network
    Qt6::Sql
)

# Translation (.ts -> .qm) pipeline. Add new languages by appending to this
# list; the Translation Manager discovers .qm files by locale code at
# runtime, so no C++ changes are required to add a language.
set(TS_FILES
    resources/translations/chronosmesh_en.ts
    resources/translations/chronosmesh_fa.ts
    resources/translations/chronosmesh_zh.ts
)
qt_add_translations(ChronosMeshDesktop TS_FILES ${TS_FILES})

install(TARGETS ChronosMeshDesktop
    RUNTIME DESTINATION bin
)

option(BUILD_TESTING "Build ChronosMesh Desktop unit tests" OFF)
if(BUILD_TESTING)
    enable_testing()
    add_subdirectory(tests)
endif()
