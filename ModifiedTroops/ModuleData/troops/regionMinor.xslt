<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='legion_of_the_betrayed_tier_1']"/>
    <xsl:template match="NPCCharacter[@id='legion_of_the_betrayed_tier_2']"/>
    <xsl:template match="NPCCharacter[@id='legion_of_the_betrayed_tier_3']"/>
    <xsl:template match="NPCCharacter[@id='company_of_the_boar_tier_1']"/>
    <xsl:template match="NPCCharacter[@id='company_of_the_boar_tier_2']"/>
    <xsl:template match="NPCCharacter[@id='company_of_the_boar_tier_3']"/>
    <xsl:template match="NPCCharacter[@id='skolderbrotva_tier_1']"/>
    <xsl:template match="NPCCharacter[@id='skolderbrotva_tier_2']"/>
    <xsl:template match="NPCCharacter[@id='skolderbrotva_tier_3']"/>
    <xsl:template match="NPCCharacter[@id='lakepike_tier_1']"/>
    <xsl:template match="NPCCharacter[@id='lakepike_tier_2']"/>
    <xsl:template match="NPCCharacter[@id='lakepike_tier_3']"/>
    <xsl:template match="NPCCharacter[@id='ghilman_tier_1']"/>
    <xsl:template match="NPCCharacter[@id='ghilman_tier_2']"/>
    <xsl:template match="NPCCharacter[@id='ghilman_tier_3']"/>

</xsl:stylesheet>
