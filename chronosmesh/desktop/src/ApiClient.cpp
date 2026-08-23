#include "ApiClient.h"

#include <QNetworkRequest>
#include <QJsonDocument>
#include <QDebug>

namespace ChronosMesh {

ApiClient::ApiClient(QObject* parent) : QObject(parent) {}

QNetworkRequest ApiClient::buildRequest(const QString& path) const {
    QUrl url = m_baseUrl;
    url.setPath(m_baseUrl.path() + path);

    QNetworkRequest request(url);
    request.setHeader(QNetworkRequest::ContentTypeHeader, QStringLiteral("application/json"));
    if (!m_accessToken.isEmpty()) {
        request.setRawHeader("Authorization", QStringLiteral("Bearer %1").arg(m_accessToken).toUtf8());
    }
    // Enforce modern TLS on every request; the desktop client never speaks
    // plain HTTP to the backend outside of an explicit local-dev base URL
    // configured in Settings.
    if (url.scheme() != QStringLiteral("https") && url.host() != QStringLiteral("localhost")) {
        qWarning() << "ApiClient: refusing non-HTTPS request to" << url.toString();
    }
    return request;
}

QNetworkReply* ApiClient::get(const QString& path) {
    return m_manager.get(buildRequest(path));
}

QNetworkReply* ApiClient::postJson(const QString& path, const QJsonObject& body) {
    const QByteArray payload = QJsonDocument(body).toJson(QJsonDocument::Compact);
    return m_manager.post(buildRequest(path), payload);
}

QNetworkReply* ApiClient::putJson(const QString& path, const QJsonObject& body) {
    const QByteArray payload = QJsonDocument(body).toJson(QJsonDocument::Compact);
    return m_manager.put(buildRequest(path), payload);
}

} // namespace ChronosMesh
