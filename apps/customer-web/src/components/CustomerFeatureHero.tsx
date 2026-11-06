export function CustomerFeatureHero({ label, title }: { label: string; title: string }) {
  return <section className="customer-feature-hero">
    <div className="customer-feature-hero-copy"><p>{label}</p><h1>{title}</h1></div>
    <div className="customer-feature-court" aria-hidden="true"><span className="customer-feature-court-net" />
      <span className="customer-feature-court-ball" /></div>
  </section>;
}
