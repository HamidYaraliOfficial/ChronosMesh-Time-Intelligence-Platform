#pragma once

#include <QObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QUrl>
#include <QJsonDocument>
#include <QJsonObject>
#include <functional>

namespace ChronosMesh {

/// Thin, secure wrapper around QNetworkAccessManager for talking to the C#
/// backend's REST API. Enforces TLS (rejects plain-HTTP base URLs outside
/// of explicit local-dev override), attaches the bearer access token, and
/// transparently refreshes via /api/v1/auth/refresh when the access token
/// has expired.
class ApiClient : public QObject {
    Q_OBJECT

public:
    explicit ApiClient(QObject* parent = nullptr);

    void setBaseUrl(const QUrl& url) { m_baseUrl = url; }
    void setAccessToken(const QString& token) { m_accessToken = token; }
    void setRefreshToken(const QString& token) { m_refreshToken = token; }

    QNetworkReply* get(const QString& path);
    QNetworkReply* postJson(const QString& path, const QJsonObject& body);
    QNetworkReply* putJson(const QString& path, const QJsonObject& body);

signals:
    void tokensRefreshed(const QString& accessToken, const QString& refreshToken);
    void authenticationExpired();

private:
    QNetworkRequest buildRequest(const QString& path) const;

    QNetworkAccessManager m_manager;
    QUrl m_baseUrl;
    QString m_accessToken;
    QString m_refreshToken;
};

} // namespace ChronosMesh
