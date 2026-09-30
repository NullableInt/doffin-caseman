FROM docker.io/library/golang:1.25-alpine AS build
WORKDIR /src
COPY go.mod go.sum ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 go build -o /out/crawler ./cmd/crawler

FROM docker.io/library/alpine:3.24
RUN apk add --no-cache ca-certificates tzdata
COPY --from=build /out/crawler /usr/local/bin/crawler
USER nobody
ENTRYPOINT ["/usr/local/bin/crawler"]
